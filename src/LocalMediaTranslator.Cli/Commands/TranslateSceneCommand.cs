using LocalMediaTranslator.Cli.Settings;
using LocalMediaTranslator.Core.Interfaces;
using LocalMediaTranslator.Core.Models;
using LocalMediaTranslator.Core.Models.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LocalMediaTranslator.Cli.Commands;

public class TranslateSceneCommand : AsyncCommand<TranslateSceneSettings> {
    private readonly IServiceProvider _serviceProvider;
    private readonly IMediaTranslationPipeline _mediaTranslationPipeline;
    private readonly IOptions<MediaClientOptions> _mediaOptions;

    public TranslateSceneCommand(IServiceProvider serviceProvider, IMediaTranslationPipeline mediaTranslationPipeline, IOptions<MediaClientOptions> options) {
        _serviceProvider = serviceProvider;
        _mediaTranslationPipeline = mediaTranslationPipeline;
        _mediaOptions = options;
    }
    public override async Task<int> ExecuteAsync(CommandContext context, TranslateSceneSettings settings, CancellationToken cancellationToken) {
        var serverType = settings.ServerType ?? _mediaOptions.Value.ServerType;
        var client = _serviceProvider.GetKeyedService<IMediaServerClient>(serverType);

        // guards
        if (client is null) {
            AnsiConsole.MarkupLine($"[red]Error:[/] No client registered for server type [bold]{serverType}[/]");
            return 1;
        }
        var scene = await client.GetSceneAsync(settings.SceneID, cancellationToken);
        if (scene is null) {
            AnsiConsole.MarkupLine($"[red]Scene not found: {Markup.Escape(settings.SceneID)}[/]");
            return 1;
        }
        if (scene.Files.Count == 0) {
            AnsiConsole.MarkupLine($"[red]Error:[/] Scene [bold]{Markup.Escape(settings.SceneID)}[/] has no associated files.");
            return 1;
        }
        var primaryFile = scene.Files[0].AbsolutePath;
        if (!File.Exists(primaryFile)) {
            AnsiConsole.MarkupLine($"[red]File not found: {Markup.Escape(primaryFile)}[/]");
            return 1;
        }

        var srtPath = !string.IsNullOrWhiteSpace(settings.OutputPath) ? settings.OutputPath : Path.ChangeExtension(primaryFile, ".srt");
        string? resultSrtPath = null;
        AnsiConsole.MarkupLine($"Found scene: [bold cyan]{Markup.Escape(scene.Title)}[/] (ID: {scene.Id})");
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
                    var extractTask = ctx.AddTask("[green]Extracting audio (FFmpeg)[/]");
                    var transcribeTask = ctx.AddTask("[yellow]Transcribing audio (Whisper)[/]");
                    var translateTask = ctx.AddTask("[cyan]Translating cues (LLM)[/]");
                    var writeTask = ctx.AddTask("[blue]Writing subtitle file[/]");

                    // UI changes
                    var progress = new Progress<PipelineProgressReport>(report => {
                        switch (report.Step) {
                            case PipelineStep.ExtractingAudio:
                                if (report.Percentage >= 100)
                                    extractTask.Increment(100);
                                break;
                            case PipelineStep.Transcribing:
                                if (!string.IsNullOrWhiteSpace(report.Message))
                                    transcribeTask.Description = $"[yellow]{Markup.Escape(report.Message)}[/]";
                                if (report.Percentage >= 100)
                                    transcribeTask.Increment(100);
                                break;
                            case PipelineStep.Translating:
                                if (report.TotalItems.HasValue)
                                    translateTask.MaxValue = report.TotalItems.Value;
                                if (report.ProcessedItems.HasValue)
                                    translateTask.Value = report.ProcessedItems.Value;
                                if (!string.IsNullOrWhiteSpace(report.Message))
                                    translateTask.Description = $"[cyan]{Markup.Escape(report.Message)}[/]";
                                break;
                            case PipelineStep.WritingSubtitles:
                                if (report.Percentage >= 100)
                                    writeTask.Increment(100);
                                break;
                        }
                    });

                    // executing logic
                    var pipelineOptions = new PipelineExecutionOptions {
                        OutputSrtPath = srtPath,
                        MediaFilePath = primaryFile,
                        LocalModelPath = settings.LocalModelPath,
                        ServerModel = settings.ServerModel,
                        DualLanguage = settings.DualLanguage
                    };
                    resultSrtPath = await _mediaTranslationPipeline.ExecuteAsync(pipelineOptions, progress, cancellationToken);
                });

            if (string.IsNullOrWhiteSpace(resultSrtPath)) {
                AnsiConsole.MarkupLine("[yellow]No spoken dialogue detected. Subtitle file was not generated.[/]");
                return 0;
            }
            AnsiConsole.MarkupLine($"[green]Successfully generated subtitles:[/] [bold]{Markup.Escape(resultSrtPath)}[/]");

            if (settings.Rescan) {
                var scanSuccess = await client.TriggerMetadataScanAsync([resultSrtPath], cancellationToken);
                if (!scanSuccess) {
                    AnsiConsole.MarkupLine($"[red]Error during Rescan:[/]");
                    return 1;
                }
            }
            return 0;
        }
        catch (Exception ex) {
            AnsiConsole.MarkupLine($"[red]Error during pipeline execution:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }
}