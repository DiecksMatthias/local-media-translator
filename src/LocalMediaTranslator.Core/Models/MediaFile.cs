namespace LocalMediaTranslator.Core.Models;

public record MediaFile(
    string Id,
    string AbsolutePath,
    string? Fingerprint,
    long? Duration,
    long? BitRate
);