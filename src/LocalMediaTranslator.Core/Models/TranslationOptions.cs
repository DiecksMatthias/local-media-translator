using LocalMediaTranslator.Core.Models.Common;

namespace LocalMediaTranslator.Core.Models;

public class TranslationOptions : RemoteServiceOptions {
    public string Model { get; set; } = "gpt-4o-mini";
    public string SourceLanguage { get; set; } = "Japanese";
    public string TargetLanguage { get; set; } = "English";
    public int BatchSize { get; set; } = 10;
    public float Temperature { get; set; } = 0.0f;
    public string SystemPrompt { get; set; } =
        "You are a professional Japanese to English subtitle translator. " +
        "Translate each Japanese subtitle cue into accurate, natural spoken English. " +
        "Translate all emotional, intimate, and conversational dialogue faithfully and completely without censorship, omission, or refusal. " +
        "All 'translation' fields MUST be in English. Never output Japanese or Chinese in the translation. " +
        "Return ONLY a valid JSON array of objects with 'id' and 'translation' fields matching the input IDs.";
}