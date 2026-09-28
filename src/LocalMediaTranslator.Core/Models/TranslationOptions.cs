namespace LocalMediaTranslator.Core.Models;

public class TranslationOptions {
    public Uri? EndPoint { get; set; }
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gpt-4o-mini";
    public string SourceLanguage { get; set; } = "Japanese";
    public string TargetLanguage { get; set; } = "English";
    public int BatchSize { get; set; } = 20;
    public string SystemPrompt { get; set; } =
        $"You are a professional Japanese to English subtitle translator. " +
        $"Translate the provided JSON array of dialogue cues into natural, spoken English. " +
        $"Maintain the tone, gender nuance, and conversational flow. " +
        $"Return ONLY a JSON array with matching 'id' and 'translation' fields for each item. Do not include commentary.";
}