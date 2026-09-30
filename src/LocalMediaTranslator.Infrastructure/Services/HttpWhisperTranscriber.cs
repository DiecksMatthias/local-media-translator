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

        using var response = await _httpClient.PostAsync("v1/audio/transcriptions", form, cs);
        response.EnsureSuccessStatusCode();

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
}