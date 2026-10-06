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
    private readonly IMediaTranslationPipeline _pipeline;
    private readonly IOptions<MediaClientOptions> _mediaOptions;

    public TranslateFileCommand(IMediaTranslationPipeline pipeline, IOptions<MediaClientOptions> mediaOptions) {
        _pipeline = pipeline;
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
        if (settings.LocalModelPath is not null && !File.Exists(settings.LocalModelPath)) {
            AnsiConsole.MarkupLine($"[red]Error[/]: Whisper model file not found: [bold] {settings.LocalModelPath}[/]");
            return 1;
        }

        var srtPath = !string.IsNullOrWhiteSpace(settings.OutputPath) ? settings.OutputPath : Path.ChangeExtension(localPath, ".srt");
        string? resultSrtPath = null;

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
                        MediaFilePath = localPath,
                        LocalModelPath = settings.LocalModelPath,
                        ServerModel = settings.ServerModel,
                        DualLanguage = settings.DualLanguage
                    };
                    resultSrtPath = await _pipeline.ExecuteAsync(pipelineOptions, progress, cancellationToken);
                });

            if (string.IsNullOrWhiteSpace(resultSrtPath)) {
                AnsiConsole.MarkupLine("[yellow]No spoken dialogue detected. Subtitle file was not generated.[/]");
                return 0;
            }
            AnsiConsole.MarkupLine($"[green]Successfully generated subtitles:[/] [bold]{Markup.Escape(resultSrtPath)}[/]");
            return 0;
        }
        catch (Exception ex) {
            AnsiConsole.MarkupLine($"[red]Error during pipeline execution:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }
}