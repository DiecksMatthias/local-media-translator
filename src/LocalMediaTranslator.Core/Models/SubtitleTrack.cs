using LocalMediaTranslator.Core.Models;

namespace LocalMediaTranslator.Core;

public class SubtitleTrack {
    public required string SourceFileName { get; set; }
    public required string Language { get; set; }
    public string? TargetedLanguage { get; set; }
    public IReadOnlyList<SubtitleItem> Items { get; set; }
}