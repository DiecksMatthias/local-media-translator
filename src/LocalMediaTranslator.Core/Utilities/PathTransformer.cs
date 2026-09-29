namespace LocalMediaTranslator.Core.Utilities;

public static class PathTransformer {
    public static string TransformPath(string remotePath, IReadOnlyDictionary<string, string>? mappings) {
        if (string.IsNullOrWhiteSpace(remotePath) || mappings == null || mappings.Count == 0)
            return remotePath;

        var normalizedPath = remotePath.Replace('\\', '/');

        foreach (var (dockerPrefix, localPrefix) in mappings.OrderByDescending(kv => kv.Key.Length)) {
            var cleanDockerPath = dockerPrefix.TrimEnd('/', '\\').Replace('\\', '/');
            if (normalizedPath.StartsWith(cleanDockerPath, StringComparison.OrdinalIgnoreCase)) {
                var relativePath = normalizedPath[cleanDockerPath.Length..].TrimStart('/', '\\');
                return Path.Combine(localPrefix, relativePath);
            }
        }
        return remotePath;
    }
}