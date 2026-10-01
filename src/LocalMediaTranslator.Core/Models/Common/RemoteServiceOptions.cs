namespace LocalMediaTranslator.Core.Models.Common;

public abstract class RemoteServiceOptions {
    public Uri? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(360);
}