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
        var handler = new MockHttpMessageHandler(mockError, System.Net.HttpStatusCode.InternalServerError);
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
}
