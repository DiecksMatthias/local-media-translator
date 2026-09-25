namespace LocalMediaTranslator.Core.Models;

public class TranslationOptions {
    public string SourceLanguage { get; set; } = "Japanese";
    public string TargetLanguage { get; set; } = "English";
    public int BatchSize { get; set; } = 20;
    public string SystemPrompt { get; set; } //TODO: needs default
}