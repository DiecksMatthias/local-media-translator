using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LocalMediaTranslator.Core.Interfaces;
using LocalMediaTranslator.Core.Models;
using LocalMediaTranslator.Core.Models.Enums;
using LocalMediaTranslator.Core.Models.Exceptions;
using LocalMediaTranslator.Infrastructure.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LocalMediaTranslator.Infrastructure.Services;

public class LlmTranslator : ITranslator {
    private readonly HttpClient _httpclient;
    private readonly IOptions<TranslationOptions> _options;
    private readonly ILogger<ITranslator> _logger;

    public LlmTranslator(HttpClient httpClient, IOptions<TranslationOptions>? options = null, ILogger<ITranslator>? logger = null) {
        _httpclient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? Options.Create(new TranslationOptions());
        _logger = logger ?? NullLogger<ITranslator>.Instance;
    }

    public async Task<IReadOnlyList<SubtitleItem>> TranslateAsync(IReadOnlyList<SubtitleItem> items, TranslationOptions options, IProgress<int>? progress = null, CancellationToken cs = default) {
        // Guards
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(options);
        if (items.Count == 0)
            return items;

        var contextHistory = new List<SubtitleItem>();
        int processCount = 0;
        int totalBatches = 0;
        int failedItems = 0;
        int failedBatches = 0;
        int consecutiveFailures = 0;
        var expectedBatches = (items.Count + options.BatchSize - 1) / options.BatchSize;
        foreach (var batch in items.Chunk(options.BatchSize)) {
            cs.ThrowIfCancellationRequested();

            try {
                totalBatches++;

                // pass last 3 items as context
                var recentContext = contextHistory.TakeLast(3).ToList();
                var translations = await TranslateBatchAsync(batch, recentContext, options, cs);

                // Duplicate IDs: the map keeps the first occurrence and the second
                // translation would vanish silently without this.
                var duplicateIds = translations
                    .GroupBy(t => t.Id)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();
                foreach (var dup in duplicateIds) {
                    _logger.LogWarning("Duplicate translation ID {id} in LLM response - keeping the first occurrence", dup);
                }

                var translationMap = new Dictionary<int, string>();
                foreach (var t in translations) {
                    if (!string.IsNullOrWhiteSpace(t.Translation)) {
                        translationMap.TryAdd(t.Id, t.Translation);
                    }
                }

                // Classify the response before assigning anything (see the decision
                // tree in .hermes/plans/subtitle-timing-fix-plan.md, Fix 1.3):
                //
                //   1. count matches, zero ID overlap  -> renumbered-but-complete:
                //      positional fallback is the only recovery, and the count match
                //      makes it safe. Log it - the LLM ignored the numbering scheme.
                //   2. short response, IDs overlap, max returned ID == max batch ID
                //      -> genuine drop: the LLM skipped a line but kept the real
                //      numbering. Trust the ID matches; the absent IDs are the misses.
                //   3. short response, IDs overlap, max returned ID < max batch ID
                //      -> renumber-and-drop suspected. BUT: an end-truncated
                //      response (LLM hit its token limit after lines 1,2 of 1,2,3)
                //      is structurally IDENTICAL to a renumbered drop, and at
                //      Temperature 0 a whole-batch re-ask deterministically returns
                //      the same truncated output - so discarding here would throw
                //      away correct translations on every truncation, the documented
                //      common failure. The matches are trusted; the missing IDs are
                //      re-asked individually (see the retry below), which is the
                //      only recovery that actually changes the input.
                //   4. short response, zero ID overlap -> nothing salvageable by ID;
                //      every item is missing.
                var batchIds = batch.Select(x => x.Index).ToHashSet();
                var returnedIds = translations.Select(t => t.Id).ToHashSet();
                var overlap = batchIds.Intersect(returnedIds);
                var countMatches = translations.Count == batch.Length;

                if (countMatches && !overlap.Any()) {
                    _logger.LogWarning(
                        "LLM returned {count} translations with no matching IDs for this batch - assigning positionally",
                        translations.Count);
                    for (int i = 0; i < batch.Length; i++) {
                        if (!string.IsNullOrWhiteSpace(translations[i].Translation)) {
                            batch[i].TranslatedText = translations[i].Translation;
                        }
                    }
                } else {
                    // Match by ID (branches 2, 3, 4)
                    foreach (var item in batch) {
                        if (translationMap.TryGetValue(item.Index, out var translatedText)) {
                            item.TranslatedText = translatedText;
                        }
                    }
                }

                // Account for unmatched items AFTER assignment, so the positional
                // fallback's recovered cues are not counted as failures.
                var unmatched = batch.Where(x => string.IsNullOrWhiteSpace(x.TranslatedText)).ToList();

                // Retry the missing IDs individually. Different input, so at
                // Temperature 0 this actually returns something new. A failed
                // re-ask does not increment consecutiveFailures.
                foreach (var item in unmatched) {
                    for (var attempt = 0; attempt < options.MaxRetries; attempt++) {
                        var retried = await RetrySingleItemAsync(item, recentContext, options, cs);
                        if (retried is not null) {
                            item.TranslatedText = retried;
                            break;
                        }
                    }
                }

                // Final accounting: whatever is still untranslated is counted and
                // logged with its source text so the miss is diagnosable.
                foreach (var item in batch) {
                    if (string.IsNullOrWhiteSpace(item.TranslatedText)) {
                        _logger.LogWarning("Untranslated Item - Index: {index}, Text: {text}", item.Index, item.OriginalText);
                        failedItems++;
                    }
                }
                // add freshly translated items to the history so they can lead as context for the next batch
                contextHistory.AddRange(batch);
                consecutiveFailures = 0;
            } catch (OperationCanceledException) when (cs.IsCancellationRequested) {
                throw;
            } catch (HttpRequestException ex) when (IsFatal(ex)) {
                _logger.LogError("Ollama encountered an Error: {message} - HTTP {statuscode}", ex.Message, ex.StatusCode);
                throw;
            } catch (Exception ex) when (ex is JsonException or HttpRequestException or TaskCanceledException) {
                foreach (var item in batch) {
                    if (item.TranslatedText is null) {
                        item.TranslatedText = item.OriginalText;
                        failedItems++;
                    }
                }
                failedBatches++;
                consecutiveFailures++;
                _logger.LogWarning(
                    "Batch {n} of {total} failed ({ex} - {reason}). Falling back to source text.",
                    failedBatches,
                    totalBatches,
                    ex.GetType(),
                    ex.Message);
                if (consecutiveFailures >= options.MaxConsecutiveFailures) {
                    var reason = failedBatches == expectedBatches
                        ? LlmTranslationFailureExceptionReasons.FullFailure
                        : LlmTranslationFailureExceptionReasons.PartialFailure;
                    throw new LlmTranslationException(
                            failedBatches,
                            expectedBatches,
                            failedItems,
                            reason,
                            $"Consecutive Failure limit of {options.MaxConsecutiveFailures} reached.",
                            inner: ex);
                }
            }

            processCount += batch.Length;
            progress?.Report(processCount);
        }

        if (failedBatches > 0 || failedItems > 0) {
            if (options.FailOnPartialFailure)
                throw new LlmTranslationException(
                    failedBatches,
                    totalBatches,
                    failedItems,
                    LlmTranslationFailureExceptionReasons.FailOnPartialFailure,
                    $"Aborted: {failedBatches} of {totalBatches} batches failed, {failedItems} cues left untranslated.");

            if (failedBatches > 0)
                _logger.LogWarning("{failed} of {total} batches fell back to source text - the output is partially untranslated.", failedBatches, totalBatches);
            if (failedItems > 0)
                _logger.LogWarning("{failed} cues were left untranslated after batch translation and retries - the output is partially untranslated.", failedItems);
        }
        return items;
    }

    private async Task<List<TranslationResponseItem>> TranslateBatchAsync(SubtitleItem[] batch, IReadOnlyList<SubtitleItem>? context, TranslationOptions options, CancellationToken cs) {

        var sb = new StringBuilder();

        // add recent translated batch as reference context
        // to keep contextual dialogues properly translated
        if (context is { Count: > 0 }) {
            sb.AppendLine("### Context from preceding dialogue (for reference only, do not translate):");
            foreach (var ctx in context) {
                // with fallback for non-translated batches
                var text = !string.IsNullOrWhiteSpace(ctx.TranslatedText) ? ctx.TranslatedText : ctx.OriginalText;
                sb.AppendLine($"- {text}");
            }
            sb.AppendLine();
        }
        sb.AppendLine("### Lines to Translate:");
        foreach (var item in batch) {
            sb.AppendLine($"{item.Index}. {item.OriginalText}");
        }

        var requestBody = new {
            model = options.Model,
            temperature = options.Temperature,
            messages = new[] {
                new { role = "system", content = options.SystemPrompt },
                new { role = "user", content = sb.ToString() }
            }
        };

        using var response = await _httpclient.PostAsJsonAsync("v1/chat/completions", requestBody, cs);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cs));
        var contentString = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
        if (string.IsNullOrWhiteSpace(contentString))
            throw new JsonException("LLM returned empty completion content.");

        return ParseResponseItems(contentString);
    }

    // Re-asks a single untranslated cue in its own small prompt. At Temperature 0
    // this genuinely changes the model's input, which is the only way a retry can
    // return something the truncated batch response did not. Returns null when the
    // re-ask fails or comes back empty - the caller's accounting loop owns the
    // failure count, so a failed re-ask must never touch consecutiveFailures.
    private async Task<string?> RetrySingleItemAsync(
        SubtitleItem item,
        IReadOnlyList<SubtitleItem> context,
        TranslationOptions options,
        CancellationToken cs) {
        try {
            var retried = await TranslateBatchAsync([item], context, options, cs);
            // Match by ID, never positionally: a single-item retry whose response
            // carries a foreign ID is garbled or stale (e.g. a truncated JSON array
            // that happens to salvage an earlier line's text) and must not be
            // assigned to this cue.
            var match = retried.FirstOrDefault(t => t.Id == item.Index && !string.IsNullOrWhiteSpace(t.Translation));
            return match?.Translation;
        } catch (Exception ex) when (ex is JsonException or HttpRequestException or TaskCanceledException) {
            _logger.LogWarning("Retry for Item - Index: {index} failed ({error})", item.Index, ex.GetType().Name);
            return null;
        }
    }

    private static List<TranslationResponseItem> ParseResponseItems(string content) {
        // Strategy 1: Numbered lines (e.g. "1. Hello", "2: World")
        var numberedMatches = ParseNumberedLines(content);
        if (numberedMatches.Count > 0)
            return numberedMatches;

        // Strategy 2: JSON array fallback
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var cleanJson = CleanJsonString(content);
        var result = JsonSerializer.Deserialize<List<TranslationResponseItem>>(cleanJson, jsonOptions);
        return result ?? throw new JsonException("Error while parsing JSON results");
    }

    private static List<TranslationResponseItem> ParseNumberedLines(string content) {
        var items = new List<TranslationResponseItem>();
        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines) {
            var match = Regex.Match(line, @"^\s*(\d+)[\.\:\-\)\s]+(.*)$");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var id)) {
                var text = match.Groups[2].Value.Trim().Trim('"', '\'');
                if (!string.IsNullOrWhiteSpace(text)) {
                    items.Add(new TranslationResponseItem(id, text));
                }
            }
        }
        return items;
    }

    private static string CleanJsonString(string content) {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        var start = content.IndexOf('[');
        if (start == -1)
            return content.Trim();

        var end = content.LastIndexOf(']');
        string json;
        if (end > start)
            json = content.Substring(start, end - start + 1);
        else {
            var lastBrace = content.LastIndexOf('}');
            if (lastBrace > start)
                json = content.Substring(start, lastBrace - start + 1) + "]";
            else
                json = content.Substring(start);
        }

        // Repair malformed {"id: 18", ...} -> {"id": 18, ...}
        json = Regex.Replace(json, @"\{\s*""id:\s*(\d+)""", @"{""id"": $1");
        // Remove trailing commas before ] or }
        json = Regex.Replace(json, @",\s*([\]\}])", "$1");

        return json;
    }

    private static bool IsFatal(HttpRequestException ex) {
        return ex.StatusCode == System.Net.HttpStatusCode.BadRequest
            || ex.StatusCode == System.Net.HttpStatusCode.Unauthorized
            || ex.StatusCode == System.Net.HttpStatusCode.Forbidden
            || ex.StatusCode == System.Net.HttpStatusCode.NotFound
            || ex.StatusCode == System.Net.HttpStatusCode.UnprocessableContent;
    }

    public async Task UnloadAsync(CancellationToken cs = default) {
        if (_options.Value.Endpoint is null || string.IsNullOrWhiteSpace(_options.Value.Model))
            return;

        try {
            // Query Ollama for all models currently occupying VRAM
            using var psResponse = await _httpclient.GetAsync("api/ps", cs);
            if (psResponse.IsSuccessStatusCode) {
                using var doc = JsonDocument.Parse(await psResponse.Content.ReadAsStreamAsync(cs));
                if (doc.RootElement.TryGetProperty("models", out var models)) {
                    foreach (var model in models.EnumerateArray()) {
                        if (model.TryGetProperty("name", out var modelName)) {
                            var nameStr = modelName.GetString();
                            if (!string.IsNullOrWhiteSpace(nameStr)) {
                                var payload = new { model = nameStr, keep_alive = 0 };
                                await _httpclient.PostAsJsonAsync("api/generate", payload, cs);
                            }
                        }
                    }
                }
            }

            // Wait until Ollama completely unloads models from VRAM (up to 10s)
            for (int i = 0; i < 20; i++) {
                await Task.Delay(500, cs);
                using var checkPs = await _httpclient.GetAsync("api/ps", cs);
                if (checkPs.IsSuccessStatusCode) {
                    using var checkDoc = JsonDocument.Parse(await checkPs.Content.ReadAsStreamAsync(cs));
                    if (checkDoc.RootElement.TryGetProperty("models", out var loadedModels) && loadedModels.GetArrayLength() == 0) {
                        break;
                    }
                }
            }
        } catch {

        }
    }
}