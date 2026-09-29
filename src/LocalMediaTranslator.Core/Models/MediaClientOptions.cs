using LocalMediaTranslator.Core.Models.Common;
using LocalMediaTranslator.Core.Models.Enums;

namespace LocalMediaTranslator.Core.Models;

public class MediaClientOptions : RemoteServiceOptions {
    public MediaServerType ServerType { get; set; }
    public Dictionary<string, string> PathMappings { get; set; } = new();

    public MediaClientOptions() {
        Endpoint = new Uri("http://localhost:9999/graphql");
    }
}