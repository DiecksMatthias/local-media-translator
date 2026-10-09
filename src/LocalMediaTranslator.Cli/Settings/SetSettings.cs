using Spectre.Console.Cli;

namespace LocalMediaTranslator.Cli.Settings;

public class SetSettings : CommandSettings {
    [CommandArgument(0, "<Section:Key>")]
    public string? Key { get; set; }

    [CommandArgument(1, "<Value>")]
    public string? Value { get; set; }
}