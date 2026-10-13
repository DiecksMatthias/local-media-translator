using System.ComponentModel;
using Spectre.Console.Cli;

namespace LocalMediaTranslator.Cli.Settings;

public class SetSettings : CommandSettings {
    [CommandArgument(0, "<Section:Key>")]
    public string? Key { get; set; }

    [CommandArgument(1, "<Value>")]
    public string? Value { get; set; }

    [CommandOption("-f|--file")]
    [Description("Config file to write to. Overrides the LMT_CONFIG_FILE environment variable")]
    public string? File { get; set; }
}
