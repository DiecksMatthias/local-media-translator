using System.Runtime.CompilerServices;
using LocalMediaTranslator.Core.Interfaces;
using LocalMediaTranslator.Core.Models;
using LocalMediaTranslator.Core.Models.Enums;
using LocalMediaTranslator.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace LocalMediaTranslator.Tests;

public class MediaTranslationPipelineTests {
    private class FakeAudioExtractor : IAudioExtractor {
        public Task<string> ExtractAudioAsync(string mediaFilePath, string outputWavPath, AudioExtractionOptions? options = null, CancellationToken cs = default) {
            File.WriteAllBytes(outputWavPath, [0x01, 0x02]);
            return Task.FromResult(outputWavPath);
        }
    }

    private class FakeTranscriber : ITranscriber {
        private readonly List<SubtitleItem> _items;

        public FakeTranscriber(List<SubtitleItem> items) {
            _items = items;
        }

        public async IAsyncEnumerable<SubtitleItem> TranscribeAsync(string audioWavPath, TranscriptionOptions options, [EnumeratorCancellation] CancellationToken cs = default) {
            foreach (var item in _items) {
                yield return item;
            }
            await Task.CompletedTask;
        }

        public Task UnloadAsync(CancellationToken cs = default) => Task.CompletedTask;
    }

    private class FakeTranslator : ITranslator {
        public IReadOnlyList<SubtitleItem>? LastReceivedItems { get; private set; }

        public Task<IReadOnlyList<SubtitleItem>> TranslateAsync(IReadOnlyList<SubtitleItem> items, TranslationOptions options, IProgress<int>? progress = null, CancellationToken cs = default) {
            LastReceivedItems = items;
            foreach (var item in items) {
                item.TranslatedText = $"Translated: {item.OriginalText}";
            }
            return Task.FromResult(items);
        }

        public Task UnloadAsync(CancellationToken cs = default) => Task.CompletedTask;
    }

    private class FakeSubtitleWriter : ISubtitleWriter {
        public SubtitleTrack? LastWrittenTrack { get; private set; }

        public Task WriteAsync(SubtitleTrack track, Stream outputStream, SubtitleFormat format = SubtitleFormat.Srt, bool dualLanguage = false, CancellationToken cs = default) {
            LastWrittenTrack = track;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ExecuteAsync_DeduplicatesConsecutiveHallucinationsAndReindexes() {
        var rawCues = new List<SubtitleItem> {
            new() { Index = 1, Start = TimeSpan.FromSeconds(0), End = TimeSpan.FromSeconds(1), OriginalText = "こんにちは" },
            new() { Index = 2, Start = TimeSpan.FromSeconds(1), End = TimeSpan.FromSeconds(2), OriginalText = "こんにちは" },
            new() { Index = 3, Start = TimeSpan.FromSeconds(2), End = TimeSpan.FromSeconds(3), OriginalText = "こんにちは" }, // 3rd repeat - dropped
            new() { Index = 4, Start = TimeSpan.FromSeconds(3), End = TimeSpan.FromSeconds(4), OriginalText = "こんにちは" }, // 4th repeat - dropped
            new() { Index = 5, Start = TimeSpan.FromSeconds(4), End = TimeSpan.FromSeconds(5), OriginalText = "世界" },       // Different text - kept
            new() { Index = 6, Start = TimeSpan.FromSeconds(5), End = TimeSpan.FromSeconds(6), OriginalText = "世界" },       // 2nd repeat - kept
            new() { Index = 7, Start = TimeSpan.FromSeconds(6), End = TimeSpan.FromSeconds(7), OriginalText = "世界" },       // 3rd repeat - dropped
            new() { Index = 8, Start = TimeSpan.FromSeconds(7), End = TimeSpan.FromSeconds(8), OriginalText = "さようなら" }   // Kept
        };

        var extractor = new FakeAudioExtractor();
        var transcriber = new FakeTranscriber(rawCues);
        var translator = new FakeTranslator();
        var writer = new FakeSubtitleWriter();

        var pipeline = new MediaTranslationPipeline(
            extractor,
            transcriber,
            translator,
            writer,
            Options.Create(new TranslationOptions()),
            Options.Create(new TranscriptionOptions())
        );

        var tempOutput = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.srt");

        try {
            var options = new PipelineExecutionOptions {
                MediaFilePath = "fake.mp4",
                OutputSrtPath = tempOutput,
                DualLanguage = true
            };

            var result = await pipeline.ExecuteAsync(options);

            Assert.NotNull(result);
            Assert.NotNull(translator.LastReceivedItems);

            // Deduplication leaves: "こんにちは" (x2), "世界" (x2), "さようなら" (x1)
            // Adjacent merging merges back-to-back unpunctuated cues:
            // -> "こんにちは こんにちは", "世界 世界", "さようなら" = 3 items
            Assert.Equal(3, translator.LastReceivedItems.Count);

            Assert.Equal("こんにちは こんにちは", translator.LastReceivedItems[0].OriginalText);
            Assert.Equal(1, translator.LastReceivedItems[0].Index);

            Assert.Equal("世界 世界", translator.LastReceivedItems[1].OriginalText);
            Assert.Equal(2, translator.LastReceivedItems[1].Index);

            Assert.Equal("さようなら", translator.LastReceivedItems[2].OriginalText);
            Assert.Equal(3, translator.LastReceivedItems[2].Index);

            Assert.NotNull(writer.LastWrittenTrack);
            Assert.Equal(3, writer.LastWrittenTrack.Items.Count);
        }
        finally {
            if (File.Exists(tempOutput)) File.Delete(tempOutput);
        }
    }

    [Fact]
    public async Task ExecuteAsync_AllWhitespaceOrEmpty_ReturnsNull() {
        var rawCues = new List<SubtitleItem> {
            new() { Index = 1, Start = TimeSpan.FromSeconds(0), End = TimeSpan.FromSeconds(1), OriginalText = "   " }
        };

        var extractor = new FakeAudioExtractor();
        var transcriber = new FakeTranscriber(rawCues);
        var translator = new FakeTranslator();
        var writer = new FakeSubtitleWriter();

        var pipeline = new MediaTranslationPipeline(
            extractor,
            transcriber,
            translator,
            writer,
            Options.Create(new TranslationOptions()),
            Options.Create(new TranscriptionOptions())
        );

        var tempOutput = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.srt");
        var options = new PipelineExecutionOptions {
            MediaFilePath = "fake.mp4",
            OutputSrtPath = tempOutput
        };

        var result = await pipeline.ExecuteAsync(options);
        Assert.Null(result);
    }

    [Fact]
    public async Task ExecuteAsync_InCueRepetition_ReducesAndDeduplicates() {
        var rawCues = new List<SubtitleItem> {
            new() { Index = 1, Start = TimeSpan.FromSeconds(0), End = TimeSpan.FromSeconds(1), OriginalText = "ちょ、ちょ、ちょ、ちょ、ちょ、" },
            new() { Index = 2, Start = TimeSpan.FromSeconds(1), End = TimeSpan.FromSeconds(2), OriginalText = "ちょ、ちょ、ちょ、ちょ、ちょ、ちょ、ちょ、" },
            new() { Index = 3, Start = TimeSpan.FromSeconds(2), End = TimeSpan.FromSeconds(3), OriginalText = "ちょ、ちょ、ちょ、" },
            new() { Index = 4, Start = TimeSpan.FromSeconds(3), End = TimeSpan.FromSeconds(4), OriginalText = "痛い痛い痛い痛い痛い" },
            new() { Index = 5, Start = TimeSpan.FromSeconds(4), End = TimeSpan.FromSeconds(5), OriginalText = "大丈夫ですか" }
        };

        var extractor = new FakeAudioExtractor();
        var transcriber = new FakeTranscriber(rawCues);
        var translator = new FakeTranslator();
        var writer = new FakeSubtitleWriter();

        var pipeline = new MediaTranslationPipeline(
            extractor,
            transcriber,
            translator,
            writer,
            Options.Create(new TranslationOptions()),
            Options.Create(new TranscriptionOptions())
        );

        var tempOutput = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.srt");

        try {
            var options = new PipelineExecutionOptions {
                MediaFilePath = "fake.mp4",
                OutputSrtPath = tempOutput
            };

            var result = await pipeline.ExecuteAsync(options);

            Assert.NotNull(result);
            Assert.NotNull(translator.LastReceivedItems);

            // Cues 1 & 2 reduce to "ちょ、ちょ、" (cue 3 dropped), then merge into "ちょ、ちょ、 ちょ、ちょ、"
            // Cues 4 & 5 ("痛い痛い", "大丈夫ですか") merge into "痛い痛い 大丈夫ですか"
            Assert.Equal(2, translator.LastReceivedItems.Count);
            Assert.Equal("ちょ、ちょ、 ちょ、ちょ、", translator.LastReceivedItems[0].OriginalText);
            Assert.Equal(1, translator.LastReceivedItems[0].Index);
            Assert.Equal("痛い痛い 大丈夫ですか", translator.LastReceivedItems[1].OriginalText);
            Assert.Equal(2, translator.LastReceivedItems[1].Index);
        }
        finally {
            if (File.Exists(tempOutput)) File.Delete(tempOutput);
        }
    }
}
