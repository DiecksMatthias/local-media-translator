using System.ComponentModel;
using LocalMediaTranslator.Core.Models.Enums;
using Spectre.Console.Cli;

namespace LocalMediaTranslator.Cli.Settings;

public class TranslateSceneSettings : CommandSettings {
    [CommandArgument(0, "<SCENE_ID>")]
    [Description("ID of the scene to fetch from the media server")]
    public string SceneID { get; init; } = string.Empty;

    [CommandOption("-s|--server-type <SERVER_TYPE>")]
    [Description("Target media server type (e.g. Stash, overrides configuration)")]
    public MediaServerType? ServerType { get; init; }

    [CommandOption("-d|--dual-language")]
    [Description("Generate dual-language subtitles (source + target text)")]
    [DefaultValue(false)]
    public bool DualLanguage { get; init; }

    [CommandOption("-r|--rescan")]
    [Description("Trigger a media server metadata rescan after subtitles are written")]
    [DefaultValue(false)]
    public bool Rescan { get; init; }

    [CommandOption("-o|--output <OUTPUT_PATH>")]
    [Description("Custom output .srt path (defaults to same name/directory as video)")]
    public string? OutputPath { get; init; }

    [CommandOption("-m|--local-model <MODEL_PATH>")]
    [Description("Path to the ggml Whisper model file (if using local Whisper)")]
    public string? LocalModelPath { get; init; }

    [CommandOption("--server-model <MODEL_NAME>")]
    [Description("Name of the ggml Whisper model")]
    public string? ServerModel { get; init; }

}