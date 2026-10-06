using System.ComponentModel;
using LocalMediaTranslator.Core.Models.Enums;
using Spectre.Console.Cli;

namespace LocalMediaTranslator.Cli.Settings;

public class DiscoverSettings : CommandSettings {
    // Execution mode 
    [CommandOption("-x|--execute")]
    [Description("Execute the translation pipeline on discovered scenes. Defaults to dry-run preview if omitted.")]
    // default value?
    public bool Execute { get; init; }
    ////////////////////////////////////////////////////////


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


    // Pipeline options
    [CommandOption("-m|--local-model <PATH>")]
    [Description("Path to the local Whisper GGML/GGUF model file (e.g. models/ggml-base.bin).")]
    public string? LocalModelPath { get; init; }

    [CommandOption("-d|--dual-language")]
    [Description("Generate bilingual subtitles showing original transcribed text above translated English.")]
    public bool DualLanguage { get; init; }

    [CommandOption("--rescan|--no-rescan")]
    [Description("Trigger a server metadata rescan for modified files after subtitle generation. Enabled by default.")]
    [DefaultValue(true)]
    public bool Rescan { get; init; } = true;
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