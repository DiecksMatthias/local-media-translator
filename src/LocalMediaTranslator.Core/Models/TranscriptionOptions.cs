using LocalMediaTranslator.Core.Models.Common;

namespace LocalMediaTranslator.Core.Models;

public class TranscriptionOptions : RemoteServiceOptions {
    public string Language { get; set; } = "ja";
    public string Model { get; init; } = "Systran/faster-whisper-large-v3";
    public string? ModelPath { get; set; }
    public float Temperature { get; set; } = 0.0f;
    public TranscriptionOptions() {
        Timeout = TimeSpan.FromMinutes(10);
    }
}