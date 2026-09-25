namespace LocalMediaTranslator.Core.Models;

public class SubtitleTrack {
    public required string SourceFileName { get; set; }
    public required LanguageCode Language { get; set; }
    public LanguageCode? TargetedLanguage { get; set; }
    public IReadOnlyList<SubtitleItem> Items { get; set; }
}