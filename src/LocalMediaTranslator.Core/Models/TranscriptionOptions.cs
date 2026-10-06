using LocalMediaTranslator.Core.Models.Common;

namespace LocalMediaTranslator.Core.Models;

public class TranscriptionOptions : RemoteServiceOptions {
    public string Language { get; set; } = "ja";

    // used for remote transcriber to identify model by id
    public string? ServerModel { get; init; }

    // used for local transcriber to point the local instance to to the model
    public string? LocalModelPath { get; set; }
    public float Temperature { get; set; } = 0.0f;
    public TranscriptionOptions() {
        Timeout = TimeSpan.FromMinutes(360);
    }
}