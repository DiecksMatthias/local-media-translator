using LocalMediaTranslator.Core.Interfaces;
using LocalMediaTranslator.Core.Models;
using LocalMediaTranslator.Core.Models.Enums;
using Microsoft.Extensions.Options;

namespace LocalMediaTranslator.Infrastructure.Services;

public class MediaTranslationPipeline : IMediaTranslationPipeline {
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

            // translate
            var translationMaxValue = subtitleItems.Count;
            var translationProgressText = "Translating cues (LLM)";
            progress?.Report(new PipelineProgressReport(PipelineStep.Translating, Percentage: 0, Message: translationProgressText));
            IProgress<int>? translationProgress = progress is null
                ? null
                : new Progress<int>(processedCount => {
                    progress.Report(new PipelineProgressReport(
                        PipelineStep.Translating, ProcessedItems: processedCount, TotalItems: subtitleItems.Count,
                        Percentage: subtitleItems.Count > 0 ? (double)processedCount / subtitleItems.Count * 100.0 : 0,
                        Message: $"Translating cues ({processedCount}/{subtitleItems.Count})"));
                });
            var translatedItems = await _translator.TranslateAsync(subtitleItems, _translationOptions.Value, translationProgress, cs);

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