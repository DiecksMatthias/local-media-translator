using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using LocalMediaTranslator.Core.Interfaces;
using LocalMediaTranslator.Core.Models;

namespace LocalMediaTranslator.Infrastructure.Services;

public class HttpWhisperTranscriber : ITranscriber {
    private readonly HttpClient _httpClient;

    public HttpWhisperTranscriber(HttpClient httpClient) {
        _httpClient = httpClient;
    }

    public async IAsyncEnumerable<SubtitleItem> TranscribeAsync(string audioWavPath, TranscriptionOptions options, [EnumeratorCancellation] CancellationToken cs = default) {
        // Guards
        if (!File.Exists(audioWavPath))
            throw new FileNotFoundException(message: $"Audio File not found at {audioWavPath}");


        using var form = new MultipartFormDataContent();
        await using var fileStream = File.OpenRead(audioWavPath);
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

        form.Add(fileContent, "file", Path.GetFileName(audioWavPath));
        form.Add(new StringContent(options.Model ?? "Systran/faster-whisper-large-v3"), "model");
        form.Add(new StringContent("verbose_json"), "response_format");
        form.Add(new StringContent(options.Language ?? "ja"), "language");
        form.Add(new StringContent("0.0"), "temperature");

        // whisper is hallucinating additional spoken word when there is no audio 
        // and these parameters should stop it from doing that
        form.Add(new StringContent("true"), "vad_filter");
        form.Add(new StringContent("false"), "condition_on_previous_text");
        form.Add(new StringContent("2.4"), "compression_ratio_threshold");
        form.Add(new StringContent("0.6"), "no_speech_threshold");

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
            using var response = await _httpClient.GetAsync("api/ps", cs);
            if (response.IsSuccessStatusCode) {
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cs));
                if (doc.RootElement.TryGetProperty("models", out var models)) {
                    foreach (var model in models.EnumerateArray()) {
                        var modelID = model.GetString();
                        if (!string.IsNullOrWhiteSpace(modelID))
                            await _httpClient.DeleteAsync($"api/ps/{Uri.EscapeDataString(modelID)}", cs);
                    }
                }
            }
        }
        catch {

        }
    }

}