namespace LocalMediaTranslator.Core.Models;

public class MediaClientOptions {
    public Uri Endpoint { get; set; } = new Uri("http://localhost:9999/graphql");
    public string? ApiKey { get; set; }
    public TimeSpan Timeout { get; set; }
}