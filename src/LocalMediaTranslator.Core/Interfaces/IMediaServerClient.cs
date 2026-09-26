using LocalMediaTranslator.Core.Models;

namespace LocalMediaTranslator.Core.Interfaces;

public interface IMediaServerClient {
    Task<MediaScene?> GetSceneAsync(string sceneId, CancellationToken cs = default);
    Task<IReadOnlyList<MediaScene>> FindScenesAsync(MediaSceneFilter filter, CancellationToken cs = default);
    Task<bool> TriggerMetadataScanAsync(IReadOnlyList<string> paths, CancellationToken cs = default);
}