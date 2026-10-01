using System.Text.RegularExpressions;
using LocalMediaTranslator.Core.Interfaces;
using LocalMediaTranslator.Core.Models;
using LocalMediaTranslator.Core.Models.Enums;
using Microsoft.Extensions.Options;

namespace LocalMediaTranslator.Infrastructure.Services;

public partial class MediaTranslationPipeline : IMediaTranslationPipeline {
    private static readonly Regex InCueRepetitionRegex = new(@"([^、,。\s!！?？]+[、,。\s!！?？]*)\1{2,}", RegexOptions.Compiled);
    private readonly IAudioExtractor _audioExtractor;
    private readonly ITranscriber _transcriber;
    private readonly ITranslator _translator;
    private readonly ISubtitleWriter _subtitleWriter;
    private readonly IOptions<TranslationOptions> _translationOptions;
    private readonly IOptions<TranscriptionOptions> _transcribeOptions;

    public MediaTranslationPipeline(IAudioExtractor audioExtractor, ITranscriber transcriber, ITranslator translator, ISubtitleWriter subtitleWriter, IOptions<TranslationOptions> translationOptions, IOptions<TranscriptionOptions> transcribeOptions) {
        _audioExtractor = audioExtractor;
        _transcriber = transcriber;
        _translator = translator;
        _subtitleWriter = subtitleWriter;
        _translationOptions = translationOptions;
        _transcribeOptions = transcribeOptions;
    }
    public async Task<string?> ExecuteAsync(PipelineExecutionOptions options, IProgress<PipelineProgressReport>? progress = null, CancellationToken cs = default) {
        var tempAudioPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");

        try {
            // extract
            progress?.Report(new PipelineProgressReport(PipelineStep.ExtractingAudio, Percentage: null, Message: "Extracting audio (FFmpeg)"));
            await _audioExtractor.ExtractAudioAsync(options.MediaFilePath, tempAudioPath, null, cs);
            progress?.Report(new PipelineProgressReport(PipelineStep.ExtractingAudio, Percentage: 100, Message: "Extracting audio (FFmpeg)"));

            // try to flush the gpu memory so it doesn't run out of memory when starting to transcribe 
            await _translator.UnloadAsync(cs);

            // transcribe
            var transcribeOptions = new TranscriptionOptions {
                ModelPath = _transcribeOptions.Value.ModelPath,
                Model = _transcribeOptions.Value.Model,
                Language = _transcribeOptions.Value.Language,
                Temperature = _transcribeOptions.Value.Temperature
            };

            var subtitleItems = new List<SubtitleItem>();
            await foreach (var item in _transcriber.TranscribeAsync(tempAudioPath, transcribeOptions, cs)) {
                subtitleItems.Add(item);
                progress?.Report(new PipelineProgressReport(PipelineStep.Transcribing, Percentage: null, Message: $"Transcribing audio (Whisper) - {subtitleItems.Count} cues"));
            }

            // flush out the transcribe model from VRAM so ollama has access to the whole gpu
            await _transcriber.UnloadAsync(cs);
            progress?.Report(new PipelineProgressReport(PipelineStep.Transcribing, Percentage: 100, Message: $"Transcribing audio (Whisper) - {subtitleItems.Count} cues"));

            if (subtitleItems.Count == 0) {
                progress?.Report(new PipelineProgressReport(PipelineStep.Transcribing, Percentage: null, Message: "No spoken dialogue detected"));
                return null;
            }

            // additional safeguard for duplicated voicelines here, ITranscriber should handle most of them
            // here only to clean up the stray dupes and in-cue repetitions (e.g. ちょ、ちょ、ちょ、...)
            var cleanedItems = new List<SubtitleItem>();
            SubtitleItem? lastItem = null;
            int consecutiveCount = 0;

            foreach (var item in subtitleItems) {
                var text = item.OriginalText?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                // Reduce in-cue repeated words/particles (e.g. ちょ、 repeated 3+ times down to 2)
                var reducedText = InCueRepetitionRegex.Replace(text, "$1$1").Trim();
                if (string.IsNullOrWhiteSpace(reducedText))
                    continue;

                item.OriginalText = reducedText;

                if (lastItem is not null && string.Equals(lastItem.OriginalText?.Trim(), reducedText, StringComparison.OrdinalIgnoreCase)) {
                    consecutiveCount++;
                    // only allow two repeats at most
                    if (consecutiveCount > 2)
                        continue;
                } else {
                    consecutiveCount = 1;
                }

                cleanedItems.Add(item);
                lastItem = item;
            }

            if (cleanedItems.Count == 0) {
                progress?.Report(new PipelineProgressReport(PipelineStep.Transcribing, Percentage: null, Message: "No spoken dialogue detected"));
                return null;
            }

            for (int i = 0; i < cleanedItems.Count; i++)
                cleanedItems[i].Index = i + 1;

            // translate
            var translationMaxValue = cleanedItems.Count;
            var translationProgressText = "Translating cues (LLM)";
            progress?.Report(new PipelineProgressReport(PipelineStep.Translating, Percentage: 0, Message: translationProgressText));
            IProgress<int>? translationProgress = progress is null
                ? null
                : new Progress<int>(processedCount => {
                    progress.Report(new PipelineProgressReport(
                        PipelineStep.Translating, ProcessedItems: processedCount, TotalItems: cleanedItems.Count,
                        Percentage: cleanedItems.Count > 0 ? (double)processedCount / cleanedItems.Count * 100.0 : 0,
                        Message: $"Translating cues ({processedCount}/{cleanedItems.Count})"));
                });
            var translatedItems = await _translator.TranslateAsync(cleanedItems, _translationOptions.Value, translationProgress, cs);

            // write
            progress?.Report(new PipelineProgressReport(PipelineStep.WritingSubtitles, Message: "Writing subtitle file"));
            await using (var fileStream = File.Create(options.OutputSrtPath)) {
                var track = new SubtitleTrack {
                    SourceFileName = Path.GetFileName(options.MediaFilePath),
                    Language = LanguageCode.Japanese, // hardcoded for now instead of default value in subtitletrack class so i can add a command option later
                    TargetedLanguage = LanguageCode.English, // hardcoded for now instead of default value in subtitletrack class so i can add a command option later
                    Items = translatedItems.ToList()
                };
                await _subtitleWriter.WriteAsync(track, fileStream, dualLanguage: options.DualLanguage, cs: cs);
                progress?.Report(new PipelineProgressReport(PipelineStep.WritingSubtitles, Percentage: 100, Message: "Writing subtitle file"));
            }
        }
        finally {
            if (File.Exists(tempAudioPath))
                File.Delete(tempAudioPath);
        }
        return options.OutputSrtPath;
    }
}