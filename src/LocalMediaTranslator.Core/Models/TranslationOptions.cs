using LocalMediaTranslator.Core.Models.Common;

namespace LocalMediaTranslator.Core.Models;

public class TranslationOptions : RemoteServiceOptions {
    public string Model { get; set; } = "gpt-4o-mini";
    public string SourceLanguage { get; set; } = "Japanese";
    public string TargetLanguage { get; set; } = "English";
    public int BatchSize { get; set; } = 10;
    public bool FailOnPartialFailure { get; set; } = false;
    public int MaxConsecutiveFailures { get; set; } = 5;
    public float Temperature { get; set; } = 0.0f;
    public string SystemPrompt { get; set; } =
        "You are a professional Japanese to English subtitle translator. " +
        "Translate the following numbered Japanese subtitle lines into natural English. " +
        "Translate accurately, faithfully, and line by line without censorship or skipping lines. " +
        "Output ONLY the translated lines with their matching numbers.";
}