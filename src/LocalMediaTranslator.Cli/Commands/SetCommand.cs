using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using LocalMediaTranslator.Cli.Settings;
using LocalMediaTranslator.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LocalMediaTranslator.Cli.Commands;


public class SetCommand : AsyncCommand<SetSettings> {
    internal record struct ParsedKey(string Section, string Property, string? DictKey);
    public override async Task<int> ExecuteAsync(CommandContext context, SetSettings settings, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(settings.Key)) {
            AnsiConsole.MarkupLine("[red]Error:[/] Key cannot be empty");
            return 1;
        }

        var parts = settings.Key.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) {
            AnsiConsole.MarkupLine("[red]Error:[/] Key must be in the Format Section:Key");
            return 1;
        }

        ParsedKey parsedKey = ParseKey(parts);
        try {
            ValidateSectionAndProperty(parsedKey);
        }
        catch (InvalidOperationException ex) {
            AnsiConsole.MarkupLine($"[red]Error during setting variables:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
        catch (Exception ex) {
            AnsiConsole.MarkupLine($"[red]Unexpected Error during setting variables:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
        if (string.IsNullOrWhiteSpace(settings.Value)) {
            AnsiConsole.MarkupLine("[red]Error:[/] Value cannot be empty");
            return 1;
        }

        var targetFile = Path.Combine(AppContext.BaseDirectory, "appsettings.local.json");
        WriteConfig(targetFile, parsedKey, settings.Value);
        return 0;
    }

    internal static ParsedKey ParseKey(string[] input) => new ParsedKey(input[0], input[1], input.Length > 2 ? input[2] : null);

    internal void ValidateSectionAndProperty(ParsedKey input) {
        var knownSections = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase) {
            ["Transcription"] = typeof(TranscriptionOptions),
            ["Translation"] = typeof(TranslationOptions),
            ["MediaServer"] = typeof(MediaClientOptions)
        };

        if (!knownSections.TryGetValue(input.Section, out var optionsType))
            throw new InvalidOperationException($"Unknown Section '{input.Section}'. Known: {string.Join(", ", knownSections.Keys)}");
        if (input.DictKey is null) {
            var propInfo = optionsType.GetProperty(input.Property, BindingFlags.Public | BindingFlags.Instance);
            if (propInfo is null)
                throw new InvalidOperationException($"Unknown key '{input.Section}:{input.Property}'. Available: {string.Join(", ", optionsType.GetProperties().Select(p => p.Name))}");
        }
    }

    internal static void WriteConfig(string targetFile, ParsedKey parsedKey, string value) {
        var json = File.Exists(targetFile) ? File.ReadAllText(targetFile) : "{}";
        var root = JsonNode.Parse(json) ?? new JsonObject();

        if (root[parsedKey.Section] is not JsonObject sectionObj)
            root[parsedKey.Section] = sectionObj = new JsonObject();

        if (parsedKey.DictKey is null) {
            sectionObj[parsedKey.Property] = value;
        } else {
            if (sectionObj[parsedKey.Property] is not JsonObject dictObj)
                sectionObj[parsedKey.Property] = dictObj = new JsonObject();
            dictObj[parsedKey.DictKey] = value;
        }

        File.WriteAllText(targetFile, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}