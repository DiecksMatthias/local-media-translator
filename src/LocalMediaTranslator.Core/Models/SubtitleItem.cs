namespace LocalMediaTranslator.Core.Models;

public class SubtitleItem {
    public int Index { get; set; }
    public TimeSpan Start { get; init; }
    public TimeSpan End { get; init; }
    public required string OriginalText { get; set; }
    public string? TranslatedText { get; set; }

    public TimeSpan Duration => End - Start;
    public string GetFormattedText(bool dual = false)
        => dual && !string.IsNullOrWhiteSpace(TranslatedText)
        ? $"{OriginalText}\n{TranslatedText}" : (TranslatedText ?? OriginalText);
}