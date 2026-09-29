using System.Net.Http.Json;
using System.Text.Json;
using LocalMediaTranslator.Core.Interfaces;
using LocalMediaTranslator.Core.Models;
using LocalMediaTranslator.Infrastructure.Models;

namespace LocalMediaTranslator.Infrastructure.Services;

public class LlmTranslator : ITranslator {
    private readonly HttpClient _httpclient;

    public LlmTranslator(HttpClient httpClient) {
        _httpclient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<IReadOnlyList<SubtitleItem>> TranslateAsync(IReadOnlyList<SubtitleItem> items, TranslationOptions options, IProgress<int>? progress = null, CancellationToken cs = default) {
        // Guards
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(options);
        if (items.Count == 0)
            return items;

        int processCount = 0;
        foreach (var batch in items.Chunk(options.BatchSize)) {
            cs.ThrowIfCancellationRequested();

            var translations = await TranslateBatchAsync(batch, options, cs);
            var translationMap = translations.ToDictionary(t => t.Id, t => t.Translation);

            foreach (var item in batch) {
                if (translationMap.TryGetValue(item.Index, out var translatedText)) {
                    item.TranslatedText = translatedText;
                }
            }
            processCount += batch.Length;
            progress?.Report(processCount);
        }
        return items;
    }

    private async Task<List<TranslationResponseItem>> TranslateBatchAsync(SubtitleItem[] batch, TranslationOptions options, CancellationToken cs) {
        var payloadItems = batch.Select(x => new TranslationRequestItem(x.Index, x.OriginalText));

        var requestBody = new {
            model = options.Model,
            messages = new[] {
                new { role = "system", content = options.SystemPrompt },
                new { role = "user", content = JsonSerializer.Serialize(payloadItems)}
            },
            //response_format = new { type = "json_object" }
        };

        using var response = await _httpclient.PostAsJsonAsync("v1/chat/completions", requestBody, cs);
        response.EnsureSuccessStatusCode();

        // parse html to get to the llm's answer
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cs));
        var contentString = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
        if (string.IsNullOrWhiteSpace(contentString))
            throw new JsonException("LLM returned empty completion content.");

        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var cleanJson = CleanJsonString(contentString);
        var result = JsonSerializer.Deserialize<List<TranslationResponseItem>>(cleanJson, jsonOptions);
        return result ?? throw new JsonException(message: "Error while parsing JSON results");
    }

    private static string CleanJsonString(string content) {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        var start = content.IndexOf('[');
        if (start == -1)
            return content.Trim(); // fallback when no array brackets are found

        var end = content.LastIndexOf(']');

        // case 1: properly formed array
        if (end > start)
            return content.Substring(start, end - start + 1);

        // case 2: array properly starting but cut off end
        var lastBrace = content.LastIndexOf('}');
        if (lastBrace > start)
            return content.Substring(start, lastBrace - start + 1) + "]";

        return content.Substring(start);
    }
}