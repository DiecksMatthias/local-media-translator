
using System.Net;
using System.Text.Json;
using LocalMediaTranslator.Core.Models;
using LocalMediaTranslator.Core.Models.Enums;
using LocalMediaTranslator.Core.Models.Exceptions;
using LocalMediaTranslator.Infrastructure.Services;
using LocalMediaTranslator.Tests.Helpers;

namespace LocalMediaTranslator.Tests;

public class LlmTranslatorTests {
    [Fact]
    public async Task TranslateAsync_NullItemsOrOptions_ThrowsArgumentNullException() {
        var handler = new MockHttpMessageHandler("{}");
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var translator = new LlmTranslator(client);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            translator.TranslateAsync(null!, new TranslationOptions()));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            translator.TranslateAsync(new List<SubtitleItem>(), null!));
    }

    [Fact]
    public async Task TranslateAsync_EmptyItems_ReturnsEmptyWithoutHttpCall() {
        var handler = new MockHttpMessageHandler("{}");
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var translator = new LlmTranslator(client);

        var result = await translator.TranslateAsync(new List<SubtitleItem>(), new TranslationOptions());

        Assert.Empty(result);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task TranslateAsync_ValidResponse_PopulatesTranslatedText() {
        var mockLlmJson = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"[{\\\"id\\\":1,\\\"translation\\\":\\\"Hello\\\"},{\\\"id\\\":2,\\\"translation\\\":\\\"Goodbye\\\"}]\"}}]}";

        var handler = new MockHttpMessageHandler(mockLlmJson);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var translator = new LlmTranslator(client);

        var items = new List<SubtitleItem> {
            new() { Index = 1, OriginalText = "こんにちは" },
            new() { Index = 2, OriginalText = "さようなら" }
        };

        var result = await translator.TranslateAsync(items, new TranslationOptions());

        Assert.Equal("Hello", result[0].TranslatedText);
        Assert.Equal("Goodbye", result[1].TranslatedText);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task TranslateAsync_MarkdownFencedResponse_ParsesSuccessfully() {
        var mockLlmJson = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"```json\\n[{\\\"id\\\":1,\\\"translation\\\":\\\"Good morning\\\"}]\\n```\"}}]}";

        var handler = new MockHttpMessageHandler(mockLlmJson);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var translator = new LlmTranslator(client);

        var items = new List<SubtitleItem> {
            new() { Index = 1, OriginalText = "おはよう" }
        };

        var result = await translator.TranslateAsync(items, new TranslationOptions());

        Assert.Equal("Good morning", result[0].TranslatedText);
    }

    [Fact]
    public async Task TranslateAsync_MultipleBatches_ChunksAndReportsProgress() {
        var mockLlmJson = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"[{\\\"id\\\":1,\\\"translation\\\":\\\"one\\\"},{\\\"id\\\":2,\\\"translation\\\":\\\"two\\\"},{\\\"id\\\":3,\\\"translation\\\":\\\"three\\\"},{\\\"id\\\":4,\\\"translation\\\":\\\"four\\\"},{\\\"id\\\":5,\\\"translation\\\":\\\"five\\\"}]\"}}]}";

        var handler = new MockHttpMessageHandler(mockLlmJson);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var translator = new LlmTranslator(client);

        var items = Enumerable.Range(1, 5)
            .Select(i => new SubtitleItem { Index = i, OriginalText = $"Text {i}" })
            .ToList();

        var options = new TranslationOptions { BatchSize = 2 };
        var progressReports = new List<int>();
        var progress = new Progress<int>(v => progressReports.Add(v));

        await translator.TranslateAsync(items, options, progress);

        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task TranslateAsync_ConversationalPreambleAndFences_ParsesSuccessfully() {
        var mockLlmJson = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"Here is the translated JSON:\\n```json\\n[{\\\"id\\\":1,\\\"translation\\\":\\\"Good morning\\\"}]\\n```\\nHope this helps!\"}}]}";

        var handler = new MockHttpMessageHandler(mockLlmJson);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var translator = new LlmTranslator(client);

        var items = new List<SubtitleItem> {
            new() { Index = 1, OriginalText = "おはよう" }
        };

        var result = await translator.TranslateAsync(items, new TranslationOptions());

        Assert.Equal("Good morning", result[0].TranslatedText);
    }

    [Fact]
    public async Task TranslateAsync_TruncatedJsonResponse_RecoversCompletedItems() {
        var mockLlmJson = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"[{\\\"id\\\":1,\\\"translation\\\":\\\"Completed item\\\"},{\\\"id\\\":2,\\\"translation\\\":\\\"Truncated\"}}]}";

        var handler = new MockHttpMessageHandler(mockLlmJson);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var translator = new LlmTranslator(client);

        var items = new List<SubtitleItem> {
            new() { Index = 1, OriginalText = "完了" },
            new() { Index = 2, OriginalText = "途切れた" }
        };

        var result = await translator.TranslateAsync(items, new TranslationOptions());

        Assert.Equal("Completed item", result[0].TranslatedText);
        Assert.Null(result[1].TranslatedText);
    }

    [Fact]
    public async Task TranslateAsync_SendsConfiguredTemperatureInPayload() {
        var mockLlmJson = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"[{\\\"id\\\":1,\\\"translation\\\":\\\"Test\\\"}]\"}}]}";
        var handler = new MockHttpMessageHandler(mockLlmJson);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var translator = new LlmTranslator(client);

        var items = new List<SubtitleItem> {
            new() { Index = 1, OriginalText = "テスト" }
        };

        var options = new TranslationOptions { Temperature = 0.0f };
        await translator.TranslateAsync(items, options);

        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("\"temperature\":0", handler.LastRequestBody);
    }

    [Fact]
    public async Task TranslateAsync_MalformedIdKeyAndTrailingCommas_RepairsAndParsesSuccessfully() {
        var mockLlmJson = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"[{\\\"id: 1\\\", \\\"translation\\\": \\\"Hello\\\"}, {\\\"id\\\": 2, \\\"translation\\\": \\\"World\\\"}, ]\"}}]}";
        var handler = new MockHttpMessageHandler(mockLlmJson);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var translator = new LlmTranslator(client);

        var items = new List<SubtitleItem> {
            new() { Index = 1, OriginalText = "こんにちは" },
            new() { Index = 2, OriginalText = "世界" }
        };

        var result = await translator.TranslateAsync(items, new TranslationOptions());

        Assert.Equal("Hello", result[0].TranslatedText);
        Assert.Equal("World", result[1].TranslatedText);
    }

    [Fact]
    public async Task TranslateAsync_DuplicateIdsInResponse_DoesNotThrowAndPopulatesTranslations() {
        var mockLlmJson = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"[{\\\"id\\\": 1, \\\"translation\\\": \\\"First\\\"}, {\\\"id\\\": 1, \\\"translation\\\": \\\"Duplicate\\\"}]\"}}]}";
        var handler = new MockHttpMessageHandler(mockLlmJson);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var translator = new LlmTranslator(client);

        var items = new List<SubtitleItem> {
            new() { Index = 1, OriginalText = "最初" }
        };

        var result = await translator.TranslateAsync(items, new TranslationOptions());

        Assert.NotNull(result[0].TranslatedText);
    }

    [Fact]
    public async Task TranslateAsync_NumberedLinesResponse_ParsesSuccessfully() {
        var mockLlmContent = "1. Hello world\n2. Goodbye friend";
        var mockLlmJson = $"{{\"choices\":[{{\"message\":{{\"role\":\"assistant\",\"content\":{JsonSerializer.Serialize(mockLlmContent)}}}}}]}}";
        var handler = new MockHttpMessageHandler(mockLlmJson);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var translator = new LlmTranslator(client);

        var items = new List<SubtitleItem> {
            new() { Index = 1, OriginalText = "こんにちは世界" },
            new() { Index = 2, OriginalText = "さようなら友よ" }
        };

        var result = await translator.TranslateAsync(items, new TranslationOptions());

        Assert.Equal("Hello world", result[0].TranslatedText);
        Assert.Equal("Goodbye friend", result[1].TranslatedText);
    }

    [Fact]
    public async Task TranslateAsync_MultipleBatches_IncludesPrecedingContextInSubsequentRequests() {
        var mockLlmContent = "1. Hello\n2. World\n3. Next";
        var mockLlmJson = $"{{\"choices\":[{{\"message\":{{\"role\":\"assistant\",\"content\":{JsonSerializer.Serialize(mockLlmContent)}}}}}]}}";
        var handler = new MockHttpMessageHandler(mockLlmJson);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var translator = new LlmTranslator(client);

        var items = new List<SubtitleItem> {
            new() { Index = 1, OriginalText = "こんにちは" },
            new() { Index = 2, OriginalText = "世界" },
            new() { Index = 3, OriginalText = "次へ" }
        };

        var options = new TranslationOptions { BatchSize = 2 };
        await translator.TranslateAsync(items, options);

        Assert.Equal(2, handler.CallCount);
        Assert.NotNull(handler.LastRequestBody);

        // Batch 2 request should include reference context from Batch 1
        Assert.Contains("Context from preceding dialogue", handler.LastRequestBody);
        Assert.Contains("Hello", handler.LastRequestBody);
    }

    // ---------------------------------------------------------------------
    // Failure-path tests.
    //
    // Before the failure handling was added, every exception below was caught
    // by a bare `catch (Exception)` and silently degraded to source text while
    // the CLI reported success. These pin the new behaviour: fatal errors
    // propagate, per-batch noise falls back and is counted, and a dead server
    // throws instead of returning an untranslated file.
    // ---------------------------------------------------------------------

    private static string LlmJson(string content) =>
        $"{{\"choices\":[{{\"message\":{{\"role\":\"assistant\",\"content\":{JsonSerializer.Serialize(content)}}}}}]}}";

    private static HttpClient ClientFor(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://localhost:11434/") };

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task TranslateAsync_FatalStatusCode_ThrowsInsteadOfFallingBack(HttpStatusCode status) {
        var handler = new MockHttpMessageHandler("{}", status);
        var translator = new LlmTranslator(ClientFor(handler));

        var items = new List<SubtitleItem> {
            new() { Index = 1, OriginalText = "こんにちは" },
     new() { Index = 2, OriginalText = "さようなら" }
     };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
    translator.TranslateAsync(items, new TranslationOptions { BatchSize = 2 }));

        Assert.Equal(status, ex.StatusCode);
        // A wrong model name or a rejected key must not be papered over: the run
        // stops on the first batch instead of grinding through all of them.
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task TranslateAsync_HungServer_TaskCanceledIsNotSwallowedByCancellationFilter() {
        // HttpClient's own Timeout raises TaskCanceledException, which derives from
        // OperationCanceledException WITHOUT cancelling the caller's token. A filter
        // of `when (!cs.IsCancellationRequested)` therefore swallowed it and returned
        // an untranslated file. This is the regression that motivated the fix.
        Func<HttpRequestMessage, HttpResponseMessage> hangUp = _ =>
             // HttpClient's own Timeout raises TaskCanceledException without
             // cancelling the caller's token -- exactly what the old filter swallowed.
             throw new TaskCanceledException("Simulated client timeout");
        var handler = new MockHttpMessageHandler(hangUp);

        var translator = new LlmTranslator(ClientFor(handler));

        var items = new List<SubtitleItem> {
 new() { Index = 1, OriginalText = "こんにちは" }
        };

        // A hung server is transient: it must fall back like any other per-batch
        // failure, NOT be rethrown as if the user had cancelled. Raising the
        // failure limit to 1 makes that fallback observable as an
        // LlmTranslationException instead of a silently untranslated file.
        var options = new TranslationOptions { BatchSize = 1, MaxConsecutiveFailures = 1 };

        var ex = await Assert.ThrowsAsync<LlmTranslationException>(() =>
   translator.TranslateAsync(items, options));

        Assert.Equal(LlmTranslationFailureExceptionReasons.FullFailure, ex.Reason);
        Assert.IsType<TaskCanceledException>(ex.InnerException);
    }

    [Fact]
    public async Task TranslateAsync_CancelledToken_PropagatesOperationCanceled() {
        var handler = new MockHttpMessageHandler(LlmJson("1. Hello"));
        var translator = new LlmTranslator(ClientFor(handler));

        var items = new List<SubtitleItem> {
            new() { Index = 1, OriginalText = "こんにちは" }
        };

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
      translator.TranslateAsync(items, new TranslationOptions(), null, cts.Token));
    }

    [Fact]
    public async Task TranslateAsync_EveryBatchFails_ThrowsFullFailureWithCorrectCounts() {
        // 4 items at BatchSize 2 = 2 batches, and MaxConsecutiveFailures 2 trips on
        // the final batch -- so failedBatches == totalBatches == FullFailure.
        var handler = new MockHttpMessageHandler(_ =>
     new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("{}") });
        var translator = new LlmTranslator(ClientFor(handler));

        var items = new List<SubtitleItem> {
            new() { Index = 1, OriginalText = "一" },
       new() { Index = 2, OriginalText = "二" },
   new() { Index = 3, OriginalText = "三" },
    new() { Index = 4, OriginalText = "四" }
        };

        var options = new TranslationOptions { BatchSize = 2, MaxConsecutiveFailures = 2 };

        var ex = await Assert.ThrowsAsync<LlmTranslationException>(() =>
                translator.TranslateAsync(items, options));

        Assert.Equal(LlmTranslationFailureExceptionReasons.FullFailure, ex.Reason);
        // These counts are the regression guard: they must be BATCH counts, not
        // the cue count, or a caller reads "2 of 2" as "2 of 4 cues".
        Assert.Equal(2, ex.FailedBatches);
        Assert.Equal(2, ex.TotalBatches);
        Assert.Equal(4, ex.FailedItems);
    }

    [Fact]
    public async Task TranslateAsync_ConsecutiveFailureLimit_ThrowsPartialFailureAndBailsEarly() {
        var handler = new MockHttpMessageHandler(_ =>
     new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("{}") });
        var translator = new LlmTranslator(ClientFor(handler));

        // 6 items at BatchSize 2 = 3 batches; the limit trips on batch 2, so the
        // run aborts before the last batch is ever requested.
        var items = Enumerable.Range(1, 6)
            .Select(i => new SubtitleItem { Index = i, OriginalText = $"cue{i}" })
      .ToList();

        var options = new TranslationOptions { BatchSize = 2, MaxConsecutiveFailures = 2 };

        var ex = await Assert.ThrowsAsync<LlmTranslationException>(() =>
            translator.TranslateAsync(items, options));

        Assert.Equal(LlmTranslationFailureExceptionReasons.PartialFailure, ex.Reason);
        Assert.Equal(2, ex.FailedBatches);
        Assert.Equal(3, ex.TotalBatches);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task TranslateAsync_ScatteredBatchFailures_FallsBackOnlyForFailedCues() {
        var call = 0;
        var handler = new MockHttpMessageHandler(_ => {
            call++;
            // Batch 2 returns content that cannot be parsed into translations.
            return call == 2
       ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(LlmJson("not a translation at all")) }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(LlmJson("1. One\n2. Two")) };
        });
        var translator = new LlmTranslator(ClientFor(handler));

        // 6 items at BatchSize 2 = 3 batches, so batch 2 failing leaves
        // batches 1 and 3 either side of it to prove they survive.
        var items = Enumerable.Range(1, 6)
   .Select(i => new SubtitleItem { Index = i, OriginalText = $"cue{i}" })
 .ToList();

        var options = new TranslationOptions { BatchSize = 2, MaxConsecutiveFailures = 5 };

        var result = await translator.TranslateAsync(items, options);

        // One bad batch must not discard the surrounding work.
        Assert.Equal(3, handler.CallCount);
        Assert.Equal("One", result[0].TranslatedText);
        Assert.Equal("Two", result[1].TranslatedText);
        Assert.Equal("cue3", result[2].TranslatedText);
        Assert.Equal("cue4", result[3].TranslatedText);
        Assert.Equal("One", result[4].TranslatedText);
        Assert.Equal("Two", result[5].TranslatedText);
    }

    [Fact]
    public async Task TranslateAsync_PartialFailureWithFailOnPartialFailure_Throws() {
        var call = 0;
        var handler = new MockHttpMessageHandler(_ => {
            call++;
            return call == 2
       ? new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("{}") }
       : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(LlmJson("1. One\n2. Two")) };
        });
        var translator = new LlmTranslator(ClientFor(handler));

        var items = Enumerable.Range(1, 4)
        .Select(i => new SubtitleItem { Index = i, OriginalText = $"cue{i}" })
       .ToList();

        var options = new TranslationOptions {
            BatchSize = 2,
            MaxConsecutiveFailures = 5,
            FailOnPartialFailure = true
        };

        var ex = await Assert.ThrowsAsync<LlmTranslationException>(() =>
    translator.TranslateAsync(items, options));

        Assert.Equal(LlmTranslationFailureExceptionReasons.FailOnPartialFailure, ex.Reason);
        Assert.Equal(1, ex.FailedBatches);
        Assert.Equal(2, ex.TotalBatches);
    }

    [Fact]
    public async Task TranslateAsync_PartialFailureWithoutFailOnPartialFailure_ReturnsItems() {
        // Default behaviour stays lenient: a partially translated file is still
        // written, but only because the caller did not opt into strict mode.
        var call = 0;
        var handler = new MockHttpMessageHandler(_ => {
            call++;
            return call == 2
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("{}") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(LlmJson("1. One\n2. Two")) };
        });
        var translator = new LlmTranslator(ClientFor(handler));

        var items = Enumerable.Range(1, 4)
            .Select(i => new SubtitleItem { Index = i, OriginalText = $"cue{i}" })
            .ToList();

        var result = await translator.TranslateAsync(items, new TranslationOptions { BatchSize = 2 });

        Assert.Equal(4, result.Count);
        // Batch 1 succeeded, so the first two cues keep real translations while
        // the failed batch falls back to source text.
        Assert.Equal("One", result[0].TranslatedText);
        Assert.Equal("Two", result[1].TranslatedText);
        Assert.Equal("cue3", result[2].TranslatedText);
        Assert.Equal("cue4", result[3].TranslatedText);
    }

    // ---------------------------------------------------------------------
    // Response-shape classification.
    //
    // The LLM is asked for N numbered lines and may return fewer (drop the
    // tail at a token limit, or skip a middle line), renumber what it kept,
    // or duplicate an ID. Before this change every one of those shapes was
    // handled by the same all-or-nothing positional fallback: missing IDs
    // were silently skipped (rendered as Japanese in the output), duplicates
    // kept the LAST translation, and a short response was never counted, so
    // FailOnPartialFailure could not see it.
    // ---------------------------------------------------------------------

    private static HttpResponseMessage OkLlm(string content) =>
        new(HttpStatusCode.OK) { Content = new StringContent(LlmJson(content)) };

    private static List<SubtitleItem> Cues(params int[] indices) =>
        indices.Select(i => new SubtitleItem { Index = i, OriginalText = $"cue{i}" }).ToList();

    [Fact]
    public async Task TranslateAsync_GenuineDrop_TrustsIdMatchesAndRetriesMissing() {
        // IDs 1 and 3 for a 1..3 batch: a real gap at 2, not renumbering.
        // The matches are trustworthy; only cue 2 needs a second ask.
        var call = 0;
        var handler = new MockHttpMessageHandler(_ => {
            call++;
            return call == 1
                ? OkLlm("[{\"id\":1,\"translation\":\"One\"},{\"id\":3,\"translation\":\"Three\"}]")
                : OkLlm("[{\"id\":2,\"translation\":\"Two\"}]");
        });
        var translator = new LlmTranslator(ClientFor(handler));

        var result = await translator.TranslateAsync(Cues(1, 2, 3),
            new TranslationOptions { BatchSize = 3, MaxRetries = 1 });

        Assert.Equal("One", result[0].TranslatedText);
        Assert.Equal("Two", result[1].TranslatedText);
        Assert.Equal("Three", result[2].TranslatedText);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task TranslateAsync_GenuineDrop_RetryAlsoFails_CountsMissingTowardFailure() {
        // The re-ask returns the same short response; the cue stays
        // untranslated and MUST surface in the failure accounting instead of
        // silently rendering as Japanese.
        var handler = new MockHttpMessageHandler(_ =>
            OkLlm("[{\"id\":1,\"translation\":\"One\"},{\"id\":3,\"translation\":\"Three\"}]"));
        var translator = new LlmTranslator(ClientFor(handler));

        var ex = await Assert.ThrowsAsync<LlmTranslationException>(() =>
            translator.TranslateAsync(Cues(1, 2, 3), new TranslationOptions {
                BatchSize = 3,
                MaxRetries = 1,
                FailOnPartialFailure = true
            }));

        Assert.Equal(1, ex.FailedItems);
    }

    [Fact]
    public async Task TranslateAsync_TruncatedTail_RetriesOnlyTheMissingCues() {
        // IDs 1,2 for a 1..3 batch: the documented max-token truncation shape.
        // The prefix matches are correct and must be kept; only cue 3 is re-asked.
        var call = 0;
        var handler = new MockHttpMessageHandler(_ => {
            call++;
            return call == 1
                ? OkLlm("[{\"id\":1,\"translation\":\"One\"},{\"id\":2,\"translation\":\"Two\"}]")
                : OkLlm("[{\"id\":3,\"translation\":\"Three\"}]");
        });
        var translator = new LlmTranslator(ClientFor(handler));

        var result = await translator.TranslateAsync(Cues(1, 2, 3),
            new TranslationOptions { BatchSize = 3, MaxRetries = 1 });

        Assert.Equal("One", result[0].TranslatedText);
        Assert.Equal("Two", result[1].TranslatedText);
        Assert.Equal("Three", result[2].TranslatedText);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task TranslateAsync_DuplicateIds_KeepsFirstTranslation() {
        // Two entries for ID 1: the first is the answer, the second the model
        // repeating itself. Last-wins silently discarded the first.
        var handler = new MockHttpMessageHandler(_ =>
            OkLlm("[{\"id\":1,\"translation\":\"First\"},{\"id\":1,\"translation\":\"Second\"}]"));
        var translator = new LlmTranslator(ClientFor(handler));

        var result = await translator.TranslateAsync(Cues(1), new TranslationOptions());

        Assert.Equal("First", result[0].TranslatedText);
    }

    [Fact]
    public async Task TranslateAsync_RenumberedComplete_PositionalFallbackStillAssigns() {
        // Foreign IDs with a matching count: the model renumbered from its own
        // scheme. Positional assignment is the only recovery — lock it.
        var handler = new MockHttpMessageHandler(_ =>
            OkLlm("[{\"id\":10,\"translation\":\"First\"},{\"id\":11,\"translation\":\"Second\"}]"));
        var translator = new LlmTranslator(ClientFor(handler));

        var result = await translator.TranslateAsync(Cues(1, 2), new TranslationOptions());

        Assert.Equal("First", result[0].TranslatedText);
        Assert.Equal("Second", result[1].TranslatedText);
    }

    [Fact]
    public async Task TranslateAsync_PartialOverlap_NeverPositionallyOverwritesMatches() {
        // One real match (ID 1) plus two foreign IDs: the match must survive
        // and the unmatched cues stay untranslated — mixing matched and
        // positionally-guessed text is what scrambles cues.
        var handler = new MockHttpMessageHandler(_ =>
            OkLlm("[{\"id\":1,\"translation\":\"One\"},{\"id\":5,\"translation\":\"Five\"},{\"id\":6,\"translation\":\"Six\"}]"));
        var translator = new LlmTranslator(ClientFor(handler));

        var result = await translator.TranslateAsync(Cues(1, 2, 3),
            new TranslationOptions { BatchSize = 3, MaxRetries = 0 });

        Assert.Equal("One", result[0].TranslatedText);
        Assert.Null(result[1].TranslatedText);
        Assert.Null(result[2].TranslatedText);
    }

    [Fact]
    public async Task TranslateAsync_MaxRetriesZero_DoesNotReAsk() {
        var call = 0;
        var handler = new MockHttpMessageHandler(_ => {
            call++;
            return OkLlm("[{\"id\":1,\"translation\":\"One\"},{\"id\":3,\"translation\":\"Three\"}]");
        });
        var translator = new LlmTranslator(ClientFor(handler));

        var result = await translator.TranslateAsync(Cues(1, 2, 3),
            new TranslationOptions { BatchSize = 3, MaxRetries = 0 });

        Assert.Equal(1, handler.CallCount);
        Assert.Null(result[1].TranslatedText);
    }

    [Fact]
    public async Task TranslateAsync_TruncatedResponse_RetryIsStrictIdMatch() {
        // The retry prompt carries the cue's ORIGINAL index. A response with
        // foreign IDs means the model ignored the numbering — assigning it
        // positionally here is exactly the scramble this change exists to
        // prevent, so the cue stays untranslated (and counted).
        var handler = new MockHttpMessageHandler(_ =>
            OkLlm("[{\"id\":1,\"translation\":\"Completed item\"}]"));
        var translator = new LlmTranslator(ClientFor(handler));

        var result = await translator.TranslateAsync(Cues(1, 2),
            new TranslationOptions { BatchSize = 2, MaxRetries = 1 });

        Assert.Equal("Completed item", result[0].TranslatedText);
        Assert.Null(result[1].TranslatedText);
        Assert.Equal(2, handler.CallCount);
    }
}
