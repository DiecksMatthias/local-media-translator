using Spectre.Console;
using Spectre.Console.Cli;
using LocalMediaTranslator.Cli.Settings;
using LocalMediaTranslator.Core.Interfaces;
using LocalMediaTranslator.Core.Models;
using LocalMediaTranslator.Core.Models.Enums;
using Microsoft.Extensions.Options;
using LocalMediaTranslator.Core.Utilities;

namespace LocalMediaTranslator.Cli.Commands;

public class TranslateFileCommand : AsyncCommand<TranslateFileSettings> {
    private readonly IAudioExtractor _audioExtractor;
    private readonly ITranscriber _transcriber;
    private readonly ITranslator _translator;
    private readonly ISubtitleWriter _writer;
    private readonly IOptions<TranslationOptions> _translationOptions;
    private readonly IOptions<MediaClientOptions> _mediaOptions;

    public TranslateFileCommand(IAudioExtractor audioExtractor, ITranscriber transcriber, ITranslator translator, ISubtitleWriter writer, IOptions<TranslationOptions> translationOptions, IOptions<MediaClientOptions> mediaOptions) {
        _audioExtractor = audioExtractor;
        _transcriber = transcriber;
        _translator = translator;
        _writer = writer;
        _translationOptions = translationOptions;
        _mediaOptions = mediaOptions;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, TranslateFileSettings settings, CancellationToken cancellationToken) {
        // guards & validation
        ArgumentNullException.ThrowIfNull(settings);
        var localPath = PathTransformer.TransformPath(settings.InputPath, _mediaOptions.Value.PathMappings);
        if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath)) {
            AnsiConsole.MarkupLine($"[red]Error:[/] Input file not found [bold] {localPath}[/]");
            return 1;
        }
        if (!File.Exists(settings.ModelPath)) {
            AnsiConsole.MarkupLine($"[red]Error[/]: Whisper model file not found: [bold] {settings.ModelPath}[/]");
            return 1;
        }

        var tempAudioPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
        var srtPath = !string.IsNullOrWhiteSpace(settings.OutputPath) ? settings.OutputPath : Path.ChangeExtension(localPath, ".srt");

        try {
            await AnsiConsole.Progress()
                .AutoClear(false)
                .Columns([
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn()
                ])
                .StartAsync(async ctx => {
                    // extract
                    var extractTask = ctx.AddTask("[green]Extracting audio (FFmpeg)[/]");
                    await _audioExtractor.ExtractAudioAsync(localPath, tempAudioPath, null, cancellationToken);
                    extractTask.Increment(100);

                    // transcribe
                    var transcribeTask = ctx.AddTask("[yellow]Transcribing audio (Whisper)[/]");
                    var transcriptionOptions = new TranscriptionOptions { ModelPath = settings.ModelPath };
                    var subtitleItems = new List<SubtitleItem>();
                    await foreach (var item in _transcriber.TranscribeAsync(tempAudioPath, transcriptionOptions, cancellationToken)) {
                        subtitleItems.Add(item);
                        transcribeTask.Description = $"[yellow]Transcribing audio (Whisper) - {subtitleItems.Count} cues [/]";
                    }
                    transcribeTask.Increment(100);

                    if (subtitleItems.Count == 0) {
                        AnsiConsole.MarkupLine("[yellow]No spoken dialogue detected[/]");
                        return;
                    }

                    // translate
                    var translationTask = ctx.AddTask("[cyan]Translating cues (LLM)[/]");
                    var translationProgress = new Progress<int>(percent => {
                        translationTask.Value = percent;
                    });
                    var translationOptions = new TranslationOptions {
                        BatchSize = _translationOptions.Value.BatchSize,
                        Model = _translationOptions.Value.Model,
                        SourceLanguage = _translationOptions.Value.SourceLanguage,
                        SystemPrompt = _translationOptions.Value.SystemPrompt,
                        TargetLanguage = _translationOptions.Value.TargetLanguage
                    };
                    var translatedItems = await _translator.TranslateAsync(subtitleItems, translationOptions, translationProgress, cancellationToken);

                    //write
                    var writeTask = ctx.AddTask("[blue]Writing subtitle file[/]");
                    await using (var fileStream = File.Create(srtPath)) {
                        var track = new SubtitleTrack {
                            SourceFileName = Path.GetFileName(localPath),
                            Language = LanguageCode.Japanese, // hardcoded for now instead of default value in subtitletrack class so i can add a command option later
                            TargetedLanguage = LanguageCode.English, // hardcoded for now instead of default value in subtitletrack class so i can add a command option later
                            Items = translatedItems.ToList()
                        };
                        await _writer.WriteAsync(track, fileStream, dualLanguage: settings.DualLanguage, cs: cancellationToken);
                    }
                    writeTask.Increment(100);
                });

        }
        catch (Exception ex) {
            AnsiConsole.MarkupLine($"[red]Error during pipeline execution:[/] {ex.Message}");
            return 1;
        }
        finally {
            if (File.Exists(tempAudioPath))
                File.Delete(tempAudioPath);
        }
        AnsiConsole.MarkupLine($"[green]Successfully generated subtitles:[/] [bold]{srtPath}[/]");
        return 0;
    }
}