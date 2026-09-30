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

    public static string ReverseTransform(string localPath, IReadOnlyDictionary<string, string>? mappings) {
        if (string.IsNullOrWhiteSpace(localPath) || mappings == null || mappings.Count == 0)
            return localPath;

        var normalizedPath = localPath.Replace('\\', '/');

        foreach (var (dockerPrefix, localPrefix) in mappings.OrderByDescending(kv => kv.Value.Length)) {
            var cleanLocalPath = localPrefix.TrimEnd('/', '\\').Replace('\\', '/');
            if (normalizedPath.StartsWith(cleanLocalPath, StringComparison.OrdinalIgnoreCase)) {
                var cleanDockerPrefix = dockerPrefix.TrimEnd('/', '\\').Replace('\\', '/');
                var relativePath = normalizedPath[cleanLocalPath.Length..].Trim('/', '\\');
                return string.IsNullOrEmpty(relativePath)
                    ? cleanDockerPrefix
                    : $"{cleanDockerPrefix}/{relativePath}";
            }
        }
        return localPath;
    }
}