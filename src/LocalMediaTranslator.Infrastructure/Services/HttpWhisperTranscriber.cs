using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using LocalMediaTranslator.Core.Interfaces;
using LocalMediaTranslator.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LocalMediaTranslator.Infrastructure.Services;

public class HttpWhisperTranscriber : ITranscriber {
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpWhisperTranscriber> _logger;

    public HttpWhisperTranscriber(HttpClient httpClient, ILogger<HttpWhisperTranscriber>? logger = null) {
        _httpClient = httpClient;
        _logger = logger ?? NullLogger<HttpWhisperTranscriber>.Instance;
    }

    public async IAsyncEnumerable<SubtitleItem> TranscribeAsync(string audioWavPath, TranscriptionOptions options, [EnumeratorCancellation] CancellationToken cs = default) {
        // Guards
        if (string.IsNullOrWhiteSpace(options.ServerModel))
            throw new InvalidOperationException(
                message: "ServerModel is required when using the HTTP transcriber. " +
                "Set Transcription:ServerModel or pass --server-model.");
        if (!File.Exists(audioWavPath))
            throw new FileNotFoundException(message: $"Audio File not found at {audioWavPath}");

        using var form = new MultipartFormDataContent();
        await using var fileStream = File.OpenRead(audioWavPath);
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");


        form.Add(fileContent, "file", Path.GetFileName(audioWavPath));
        form.Add(new StringContent(options.ServerModel), "model");
        form.Add(new StringContent("verbose_json"), "response_format");
        form.Add(new StringContent(
            !string.IsNullOrWhiteSpace(options.Language)
                ? options.Language
                : TranscriptionOptions.DefaultLanguage
        ), "language");
        form.Add(new StringContent(options.Temperature.ToString()), "temperature");

        // whisper is hallucinating additional spoken word when there is no audio 
        // and these parameters should stop it from doing that
        form.Add(new StringContent("true"), "word_timestamps");
        form.Add(new StringContent("true"), "vad_filter");

        // additional vad parameters to decrease duration of segments if there is long silence between sentences
        form.Add(new StringContent("threshold:0.30"), "vad_parameters");
        form.Add(new StringContent("min_speech_duration_ms:150"), "vad_parameters");
        form.Add(new StringContent("min_silence_duration_ms:800"), "vad_parameters");
        form.Add(new StringContent("speech_pad_ms:400"), "vad_parameters");

        form.Add(new StringContent("false"), "condition_on_previous_text");
        form.Add(new StringContent("2.4"), "compression_ratio_threshold");
        form.Add(new StringContent("0.85"), "no_speech_threshold");
        form.Add(new StringContent("2.0"), "hallucination_silence_threshold");
        form.Add(new StringContent("1.2"), "repetition_penalty");

        using var response = await _httpClient.PostAsync("v1/audio/transcriptions", form, cs);
        if (!response.IsSuccessStatusCode) {
            var errorBody = await response.Content.ReadAsStringAsync(cs);
            if (errorBody.Contains("CUDA failed with error out of memory", StringComparison.OrdinalIgnoreCase) ||
                errorBody.Contains("out of memory", StringComparison.OrdinalIgnoreCase)) {
                throw new OutOfMemoryException("Remote Whisper GPU out of memory. Check if other GPU workloads (e.g. Ollama) are holding VRAM.");
            }
            throw new HttpRequestException($"Transcription failed with status {(int)response.StatusCode} ({response.ReasonPhrase}): {errorBody}");
        }
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cs));
        var segments = doc.RootElement.GetProperty("segments").EnumerateArray();

        int index = 1;
        foreach (var seg in segments) {
            var text = seg.GetProperty("text").GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var startSeconds = seg.GetProperty("start").GetDouble();
            var endSeconds = seg.GetProperty("end").GetDouble();

            // check if word-level timestamps are present
            if (seg.TryGetProperty("words", out var wordsElement)
               && wordsElement.ValueKind == JsonValueKind.Array
               && wordsElement.GetArrayLength() > 0) {
                var count = wordsElement.GetArrayLength();
                var lastWord = wordsElement[count - 1];
                if (lastWord.TryGetProperty("end", out var lastWordEnd))
                    // add a 300ms buffer 
                    endSeconds = Math.Min(lastWordEnd.GetDouble() + 0.3, endSeconds);
            }
            yield return new SubtitleItem {
                Index = index++,
                Start = TimeSpan.FromSeconds(startSeconds),
                End = TimeSpan.FromSeconds(endSeconds),
                OriginalText = text
            };
        }
    }

    // used to properly unload speachers from the vram so that ollama can use the full gpu
    public async Task UnloadAsync(CancellationToken cs = default) {
        try {
            string ressourceEndpoint = "api/ps";
            using var response = await _httpClient.GetAsync(ressourceEndpoint, cs);

            // should work with speaches, but this guard is there if there are changes in the endpoint structure
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) {
                _logger?.LogDebug("Whisper endpoint does not support {ressourceEndpoint}. Skipped Unload", ressourceEndpoint);
                return;
            }
            if (!response.IsSuccessStatusCode) {
                _logger?.LogWarning("Failed to query loaded Whisper models: HTTP {statusCode}", response.StatusCode);
                return;
            }

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cs), cancellationToken: cs);
            var root = doc.RootElement;
            var models = root.ValueKind switch {
                JsonValueKind.Array => root,
                JsonValueKind.Object when root.TryGetProperty("data", out var d) => d,
                JsonValueKind.Object when root.TryGetProperty("models", out var m) => m,
                _ => root
            };

            // nothing actually loaded yet
            if (models.ValueKind != JsonValueKind.Array)
                return;

            foreach (var model in models.EnumerateArray()) {
                var modelId = model.ValueKind switch {
                    JsonValueKind.String => model.GetString(),
                    JsonValueKind.Object when model.TryGetProperty("id", out var i) => i.GetString(),
                    _ => null
                };

                if (string.IsNullOrWhiteSpace(modelId)) continue;
                _logger?.LogDebug("Unloading Whisper model: {modelId}", modelId);
                using var delResponse = await _httpClient.DeleteAsync($"{ressourceEndpoint}/{Uri.EscapeDataString(modelId)}", cs);

                if (!delResponse.IsSuccessStatusCode)
                    _logger?.LogWarning("Failed to unload model {modelId}: HTTP {statusCode}", modelId, delResponse.StatusCode);
            }
        }
        catch (OperationCanceledException) {
            // throw to console
            throw;
        }
        catch (Exception ex) {
            _logger?.LogWarning(ex, "Whisper model unload encountered an unexpected error");
        }
    }

}