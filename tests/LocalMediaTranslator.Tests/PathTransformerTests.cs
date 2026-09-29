using LocalMediaTranslator.Core.Utilities;

namespace LocalMediaTranslator.Tests;

public class PathTransformerTests {
    [Fact]
    public void TransformPath_EmptyPathOrNullMappings_ReturnsOriginalPath() {
        Assert.Equal("", PathTransformer.TransformPath("", null));
        Assert.Equal("test/path", PathTransformer.TransformPath("test/path", null));
        Assert.Equal("test/path", PathTransformer.TransformPath("test/path", new Dictionary<string, string>()));
    }

    [Fact]
    public void TransformPath_MatchingDockerPrefix_MapsToHostPrefix() {
        var mappings = new Dictionary<string, string> {
            ["/data/videos"] = "/mnt/storage/media/videos",
            ["/data"] = "/mnt/storage"
        };

        var result = PathTransformer.TransformPath("/data/videos/movie.mp4", mappings);

        Assert.Equal(Path.Combine("/mnt/storage/media/videos", "movie.mp4"), result);
    }

    [Fact]
    public void TransformPath_LongestPrefixMatchesFirst() {
        var mappings = new Dictionary<string, string> {
            ["/data"] = "/mnt/root",
            ["/data/sub/nested"] = "/mnt/nested"
        };

        var result = PathTransformer.TransformPath("/data/sub/nested/file.mkv", mappings);

        Assert.Equal(Path.Combine("/mnt/nested", "file.mkv"), result);
    }

    [Fact]
    public void TransformPath_NonMatchingPath_ReturnsOriginalPath() {
        var mappings = new Dictionary<string, string> {
            ["/data/"] = "/mnt/storage/"
        };

        var result = PathTransformer.TransformPath("/other/path/file.mp4", mappings);

        Assert.Equal("/other/path/file.mp4", result);
    }
}
