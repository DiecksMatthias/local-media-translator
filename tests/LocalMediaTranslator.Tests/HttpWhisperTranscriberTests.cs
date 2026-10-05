using System.Net;
using System.Text;
using LocalMediaTranslator.Core.Models;
using LocalMediaTranslator.Infrastructure.Services;
using LocalMediaTranslator.Tests.Helpers;

namespace LocalMediaTranslator.Tests;

public class HttpWhisperTranscriberTests {
    [Fact]
    public async Task TranscribeAsync_FileNotFound_ThrowsFileNotFoundException() {
        var handler = new MockHttpMessageHandler("{}");
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000/") };
        var transcriber = new HttpWhisperTranscriber(client);

        var options = new TranscriptionOptions();
        var fakePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");

        await Assert.ThrowsAsync<FileNotFoundException>(async () => {
            await foreach (var _ in transcriber.TranscribeAsync(fakePath, options)) { }
        });
    }

    [Fact]
    public async Task TranscribeAsync_ValidVerboseJsonResponse_YieldsSubtitleItems() {
        var mockJson = """
        {
            "task": "transcribe",
            "language": "ja",
            "duration": 5.0,
            "text": "こんにちは世界",
            "segments": [
                {
                    "id": 1,
                    "seek": 0,
                    "start": 0.5,
                    "end": 2.5,
                    "text": " こんにちは",
                    "tokens": [1, 2, 3]
                },
                {
                    "id": 2,
                    "seek": 0,
                    "start": 2.8,
                    "end": 4.5,
                    "text": " 世界",
                    "tokens": [4, 5, 6]
                }
            ]
        }
        """;

        var handler = new MockHttpMessageHandler(mockJson);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000/") };
        var transcriber = new HttpWhisperTranscriber(client);

        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
        await File.WriteAllBytesAsync(tempFile, new byte[] { 0x01, 0x02 });

        try {
            var options = new TranscriptionOptions { Model = "Systran/faster-whisper-large-v3", Language = "ja" };
            var results = new List<SubtitleItem>();

            await foreach (var item in transcriber.TranscribeAsync(tempFile, options)) {
                results.Add(item);
            }

            Assert.Equal(2, results.Count);
            Assert.Equal(1, results[0].Index);
            Assert.Equal("こんにちは", results[0].OriginalText);
            Assert.Equal(TimeSpan.FromSeconds(0.5), results[0].Start);
            Assert.Equal(TimeSpan.FromSeconds(2.5), results[0].End);

            Assert.Equal(2, results[1].Index);
            Assert.Equal("世界", results[1].OriginalText);
            Assert.Equal(TimeSpan.FromSeconds(2.8), results[1].Start);
            Assert.Equal(TimeSpan.FromSeconds(4.5), results[1].End);

            Assert.Equal(1, handler.CallCount);
        }
        finally {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task TranscribeAsync_SendsAntiHallucinationAndVadParameters() {
        var mockJson = """{"task":"transcribe","language":"ja","duration":1.0,"text":"test","segments":[]}""";
        var handler = new MockHttpMessageHandler(mockJson);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000/") };
        var transcriber = new HttpWhisperTranscriber(client);

        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
        await File.WriteAllBytesAsync(tempFile, new byte[] { 0x01, 0x02 });

        try {
            var options = new TranscriptionOptions { Model = "Systran/faster-whisper-large-v3", Language = "ja" };
            await foreach (var _ in transcriber.TranscribeAsync(tempFile, options)) { }

            Assert.NotNull(handler.LastRequestBody);
            Assert.Contains("name=vad_filter", handler.LastRequestBody);
            Assert.Contains("true", handler.LastRequestBody);
            Assert.Contains("name=condition_on_previous_text", handler.LastRequestBody);
            Assert.Contains("false", handler.LastRequestBody);
            Assert.Contains("name=compression_ratio_threshold", handler.LastRequestBody);
            Assert.Contains("2.4", handler.LastRequestBody);
            Assert.Contains("name=no_speech_threshold", handler.LastRequestBody);
            Assert.Contains("0.85", handler.LastRequestBody);
            Assert.Contains("name=hallucination_silence_threshold", handler.LastRequestBody);
            Assert.Contains("2.0", handler.LastRequestBody);
            Assert.Contains("name=repetition_penalty", handler.LastRequestBody);
            Assert.Contains("1.2", handler.LastRequestBody);
        }
        finally {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task TranscribeAsync_CudaOutOfMemory_ThrowsOutOfMemoryException() {
        var mockError = "RuntimeError: CUDA failed with error out of memory";
        var handler = new MockHttpMessageHandler(mockError, HttpStatusCode.InternalServerError);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000/") };
        var transcriber = new HttpWhisperTranscriber(client);

        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
        await File.WriteAllBytesAsync(tempFile, new byte[] { 0x01, 0x02 });

        try {
            var options = new TranscriptionOptions();
            var ex = await Assert.ThrowsAsync<OutOfMemoryException>(async () => {
                await foreach (var _ in transcriber.TranscribeAsync(tempFile, options)) { }
            });

            Assert.Contains("Remote Whisper GPU out of memory", ex.Message);
        }
        finally {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    // Speaches' documented RunningModelsResponse (GET /api/ps, schema v0.8.3):
    // {"models": ["<model_id>", ...]} — an array of plain STRINGS, not objects.
    // This is the shape the real server returns, so it is the primary fixture.
    private const string SpeachesRunningModelsJson =
        """{"models":["Systran/faster-whisper-large-v3","openai/whisper-tiny"]}""";

    /// <summary>
    /// Builds a mock Speaches server: GET api/ps returns <paramref name="psJson"/>,
    /// every DELETE is recorded and answered with <paramref name="deleteStatus"/>.
    /// </summary>
    private static MockHttpMessageHandler CreateSpeachesHandler(
        string psJson,
        HttpStatusCode deleteStatus = HttpStatusCode.OK,
        List<string>? deleteRequests = null) {
        return new MockHttpMessageHandler(request => {
            if (request.Method == HttpMethod.Get) {
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(psJson, Encoding.UTF8, "application/json")
                };
            }

            if (request.Method == HttpMethod.Delete) {
                deleteRequests?.Add(request.RequestUri?.ToString() ?? string.Empty);
                return new HttpResponseMessage(deleteStatus);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
    }

    private static HttpWhisperTranscriber CreateTranscriber(HttpMessageHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000/") });

    [Fact]
    public async Task UnloadAsync_DocumentedSpeachesShape_UnloadsEachModelById() {
        var deleteRequests = new List<string>();
        var handler = CreateSpeachesHandler(SpeachesRunningModelsJson, deleteRequests: deleteRequests);
        var transcriber = CreateTranscriber(handler);

        await transcriber.UnloadAsync();

        // GET + one DELETE per loaded model. Model IDs contain '/', so they must be escaped.
        Assert.Equal(3, handler.CallCount);
        Assert.Equal(2, deleteRequests.Count);
        Assert.Contains("api/ps/Systran%2Ffaster-whisper-large-v3", deleteRequests[0]);
        Assert.Contains("api/ps/openai%2Fwhisper-tiny", deleteRequests[1]);
    }

    [Fact]
    public async Task UnloadAsync_DocumentedSpeachesShape_QueriesApiPsBeforeDeleting() {
        var handler = CreateSpeachesHandler(SpeachesRunningModelsJson);
        var transcriber = CreateTranscriber(handler);

        await transcriber.UnloadAsync();

        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.EndsWith("/api/ps", handler.Requests[0].RequestUri?.AbsolutePath);
        Assert.All(handler.Requests.Skip(1), r => Assert.Equal(HttpMethod.Delete, r.Method));
    }

    [Theory]
    // Documented shape (array of strings) — the one that actually occurs.
    [InlineData("""{"models":["whisper-medium"]}""", "api/ps/whisper-medium")]
    // Defensive variants for other/older builds. Element kind drives parsing.
    [InlineData("""{"models":[{"id":"whisper-medium"}]}""", "api/ps/whisper-medium")]
    [InlineData("""{"data":[{"id":"whisper-medium"}]}""", "api/ps/whisper-medium")]
    [InlineData("""[{"id":"whisper-medium"}]""", "api/ps/whisper-medium")]
    [InlineData("""["whisper-medium"]""", "api/ps/whisper-medium")]
    public async Task UnloadAsync_ModelListShapeVariations_UnloadsModel(string psJson, string expectedDeleteFragment) {
        var deleteRequests = new List<string>();
        var handler = CreateSpeachesHandler(psJson, deleteRequests: deleteRequests);
        var transcriber = CreateTranscriber(handler);

        await transcriber.UnloadAsync();

        Assert.Single(deleteRequests);
        Assert.Contains(expectedDeleteFragment, deleteRequests[0]);
    }

    [Theory]
    // No model loaded yet — nothing to unload, and no DELETE should be attempted.
    [InlineData("""{"models":[]}""")]
    [InlineData("""{"data":[]}""")]
    // Non-array payloads must bail out instead of throwing or guessing.
    [InlineData("""{"models":null}""")]
    [InlineData("""{"unexpected":"shape"}""")]
    [InlineData("""{}""")]
    public async Task UnloadAsync_NoModelsLoaded_SendsNoDelete(string psJson) {
        var handler = CreateSpeachesHandler(psJson);
        var transcriber = CreateTranscriber(handler);

        await transcriber.UnloadAsync();

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task UnloadAsync_EndpointNotFound_CompletesGracefullyWithoutDelete() {
        var handler = new MockHttpMessageHandler("Not Found", HttpStatusCode.NotFound);
        var transcriber = CreateTranscriber(handler);

        await transcriber.UnloadAsync();

        Assert.Equal(1, handler.CallCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task UnloadAsync_HttpErrorOnGet_CompletesGracefullyWithoutDelete(HttpStatusCode statusCode) {
        var handler = new MockHttpMessageHandler("upstream failure", statusCode);
        var transcriber = CreateTranscriber(handler);

        await transcriber.UnloadAsync();

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task UnloadAsync_MalformedJson_CompletesGracefullyWithoutThrowing() {
        var handler = new MockHttpMessageHandler("{not json", HttpStatusCode.OK);
        var transcriber = CreateTranscriber(handler);

        await transcriber.UnloadAsync();

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task UnloadAsync_DeleteFails_ContinuesWithRemainingModels() {
        // First DELETE fails; the second model must still be attempted so one
        // stuck model cannot block the GPU handoff for everything else.
        var attemptOrder = new List<string>();
        var handler = new MockHttpMessageHandler(request => {
            if (request.Method == HttpMethod.Get) {
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(SpeachesRunningModelsJson, Encoding.UTF8, "application/json")
                };
            }

            attemptOrder.Add(request.RequestUri?.ToString() ?? string.Empty);
            var isFirst = attemptOrder.Count == 1;
            return new HttpResponseMessage(isFirst ? HttpStatusCode.InternalServerError : HttpStatusCode.OK);
        });
        var transcriber = CreateTranscriber(handler);

        await transcriber.UnloadAsync();

        Assert.Equal(2, attemptOrder.Count);
    }

    [Fact]
    public async Task UnloadAsync_CancellationRequested_RethrowsOperationCanceledException() {
        var handler = new MockHttpMessageHandler(SpeachesRunningModelsJson);
        var transcriber = CreateTranscriber(handler);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => {
            await transcriber.UnloadAsync(cts.Token);
        });
    }
}