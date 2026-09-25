namespace LocalMediaTranslator.Core;

public class TranscriptionOptions {
    public string Language { get; set; } = "ja";
    public required string ModelPath { get; set; }
    public float Temperature { get; set; }
}