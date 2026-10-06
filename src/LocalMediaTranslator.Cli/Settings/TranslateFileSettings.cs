using System.ComponentModel;
using Spectre.Console.Cli;

namespace LocalMediaTranslator.Cli.Settings;

public class TranslateFileSettings : CommandSettings {
    [CommandArgument(0, "<INPUT_PATH>")]
    [Description("Path to the video or audio file to transcribe/translate")]
    public string InputPath { get; init; } = string.Empty;

    [CommandOption("-m|--local-model <MODEL_PATH>")]
    [Description("Path to the ggml Whisper model file")]
    public string? LocalModelPath { get; init; }

    [CommandOption("-d|--dual-language")]
    [Description("Generate dual-language subtitles (sources + target text)")]
    [DefaultValue(false)]
    public bool DualLanguage { get; init; }

    [CommandOption("-o|--output <OUTPUT_PATH>")]
    [Description("Custom output .srt path (defaults to same name/directory as video)")]
    public string? OutputPath { get; init; }
}