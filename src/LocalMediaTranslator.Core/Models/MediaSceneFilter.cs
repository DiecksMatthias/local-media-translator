namespace LocalMediaTranslator.Core.Models;

public record MediaSceneFilter(
    IReadOnlyList<string>? TagIds = null,
    string? SearchTerm = null,
    bool? HasCaption = null,
    int Page = 1,
    int PerPage = 25
);