
using System.Text.Json;
using LocalMediaTranslator.Core.Models;
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
}
