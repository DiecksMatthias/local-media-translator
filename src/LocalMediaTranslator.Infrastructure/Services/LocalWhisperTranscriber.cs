using System.Runtime.CompilerServices;
using LocalMediaTranslator.Core.Interfaces;
using LocalMediaTranslator.Core.Models;
using Whisper.net;

namespace LocalMediaTranslator.Infrastructure.Services;

public class LocalWhisperTranscriber : ITranscriber {
    public async IAsyncEnumerable<SubtitleItem> TranscribeAsync(string audioWavPath, TranscriptionOptions options, [EnumeratorCancellation] CancellationToken cs = default) {
        // Guards
        if (!File.Exists(audioWavPath))
            throw new FileNotFoundException(message: $"Audio File not found at {audioWavPath}");
        if (!File.Exists(options.ModelPath))
            throw new FileNotFoundException(message: $"Model File not found at {options.ModelPath}");

        using var factory = WhisperFactory.FromPath(options.ModelPath);
        using var process = factory.CreateBuilder().WithLanguage(options.Language).WithTemperature(options.Temperature).Build();
        await using var audioStream = File.OpenRead(audioWavPath);

        int index = 1;
        await foreach(var segment in process.ProcessAsync(audioStream, cs)) {
            var text = segment.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                continue;
            yield return new SubtitleItem {
                Index = index++,
                Start = segment.Start,
                End = segment.End,
                OriginalText = text
            };
        }
    }
}