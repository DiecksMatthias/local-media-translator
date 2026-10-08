using System.ComponentModel;
using LocalMediaTranslator.Core.Models.Enums;
using Spectre.Console.Cli;

namespace LocalMediaTranslator.Cli.Settings;

public class DiscoverSettings : CommandSettings {
    // Server & Discovery filters 
    [CommandOption("-q|--search <QUERY>")]
    public string? SearchTerm { get; init; }

    [CommandOption("-t|--tag <TAG>")]
    [Description("Filter media items by one or more tag names (e.g. -t NeedsSubtitles -t Japanese).")]
    public string[]? Tags { get; init; }

    [CommandOption("--missing-captions-only")]
    [Description("Only process scenes that do not currently have subtitle tracks.")]
    public bool? MissingCaptionsOnly { get; init; }

    [CommandOption("-l|--limit <COUNT>")]
    [Description("Maximum number of scenes to fetch and process in a single run.")]
    public int? Limit { get; init; }
    ////////////////////////////////////////////////////////


    // Server Configuration
    [CommandOption("-s|--server-type <TYPE>")]
    [Description("Type of media server to connect to (e.g. Stash, Jellyfin). Defaults to config if omitted.")]
    public MediaServerType? MediaServerType { get; init; }

    [CommandOption("-e|--endpoint <URL>")]
    [Description("Base URL / API endpoint for the media server (e.g. http://localhost:9999/graphql).")]
    public string? Endpoint { get; init; }

    [CommandOption("-k|--api-key <KEY>")]
    [Description("API key or bearer token used for authenticating with the media server.")]
    public string? ApiKey { get; init; }
    ////////////////////////////////////////////////////////
}