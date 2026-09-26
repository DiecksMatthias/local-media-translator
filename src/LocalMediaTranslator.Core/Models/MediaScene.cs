namespace LocalMediaTranslator.Core.Models;

public record MediaScene(
    string Id,
    string Title,
    IReadOnlyList<MediaFile> Files,
    IReadOnlyList<string> Tags
);