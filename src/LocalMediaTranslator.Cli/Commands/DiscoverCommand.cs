using LocalMediaTranslator.Cli.Settings;
using LocalMediaTranslator.Core.Interfaces;
using LocalMediaTranslator.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LocalMediaTranslator.Cli.Commands;

public class DiscoverCommand : AsyncCommand<DiscoverSettings> {
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<MediaClientOptions> _options;

    public DiscoverCommand(IServiceProvider serviceProvider, IOptions<MediaClientOptions> options) {
        _serviceProvider = serviceProvider;
        _options = options;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, DiscoverSettings settings, CancellationToken cancellationToken) {
        var serverType = settings.MediaServerType ?? _options.Value.ServerType;
        var client = _serviceProvider.GetKeyedService<IMediaServerClient>(serverType);
        if (client is null) {
            AnsiConsole.MarkupLine($"[red]Error:[/] No client registered for server type [bold]{serverType}[/]");
            return 1;
        }

        var sceneFilter = new MediaSceneFilter {
            SearchTerm = settings.SearchTerm,
            TagIds = settings.Tags,
            HasCaption = settings.MissingCaptionsOnly.HasValue ? !settings.MissingCaptionsOnly.Value : null,
            PerPage = settings.Limit ?? 20
        };
        var scenes = await client.FindScenesAsync(sceneFilter, cancellationToken);
        if (scenes.Count == 0) {
            AnsiConsole.MarkupLine($"[yellow]No scenes found matching the specific criteria[/]");
            return 0;
        }

        var sceneTable = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold blue]Discovered Media Scenes[/]");
        sceneTable.AddColumn(new TableColumn("[cyan]ID[/]").Centered());
        sceneTable.AddColumn(new TableColumn("[green]Title[/]"));
        sceneTable.AddColumn(new TableColumn("[yellow]Files[/]"));
        sceneTable.AddColumn(new TableColumn("[magenta]Tags[/]"));
        foreach (var scene in scenes) {
            var id = scene.Id;
            var title = Markup.Escape(scene.Title);
            var fileCount = scene.Files.Count.ToString();
            var tags = scene.Tags.Count > 0 ? Markup.Escape(string.Join(", ", scene.Tags)) : "[grey]None[/]";
            sceneTable.AddRow(id, title, fileCount, tags);
        }
        AnsiConsole.Write(sceneTable);
        return 0;
    }
}