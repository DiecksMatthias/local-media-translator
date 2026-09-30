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

    [Fact]
    public void ReverseTransform_EmptyPathOrNullMappings_ReturnsOriginalPath() {
        Assert.Equal("", PathTransformer.ReverseTransform("", null));
        Assert.Equal("test/path", PathTransformer.ReverseTransform("test/path", null));
        Assert.Equal("test/path", PathTransformer.ReverseTransform("test/path", new Dictionary<string, string>()));
    }

    [Fact]
    public void ReverseTransform_MatchingHostPrefix_MapsToDockerPrefix() {
        var mappings = new Dictionary<string, string> {
            ["/data/videos"] = "/mnt/storage/media/videos",
            ["/data"] = "/mnt/storage"
        };

        var result = PathTransformer.ReverseTransform("/mnt/storage/media/videos/movie.srt", mappings);

        Assert.Equal("/data/videos/movie.srt", result);
    }

    [Fact]
    public void ReverseTransform_LongestHostPrefixMatchesFirst() {
        var mappings = new Dictionary<string, string> {
            ["/data"] = "/mnt/root",
            ["/data/sub/nested"] = "/mnt/root/nested"
        };

        var result = PathTransformer.ReverseTransform("/mnt/root/nested/file.srt", mappings);

        Assert.Equal("/data/sub/nested/file.srt", result);
    }

    [Fact]
    public void ReverseTransform_NonMatchingPath_ReturnsOriginalPath() {
        var mappings = new Dictionary<string, string> {
            ["/data"] = "/mnt/storage"
        };

        var result = PathTransformer.ReverseTransform("/other/path/file.srt", mappings);

        Assert.Equal("/other/path/file.srt", result);
    }

    [Fact]
    public void ReverseTransform_HandlesWindowsBackslashesProperly() {
        var mappings = new Dictionary<string, string> {
            ["/data"] = "C:\\Media\\StashData"
        };

        var result = PathTransformer.ReverseTransform("C:\\Media\\StashData\\Subfolder\\file.srt", mappings);

        Assert.Equal("/data/Subfolder/file.srt", result);
    }
}
