using LocalMediaTranslator.Infrastructure.Services;

namespace LocalMediaTranslator.Tests;

public class FFmpegAudioExtractorTests {
    private readonly FFmpegAudioExtractor _extractor = new();

    [Fact]
    public async Task ExtractAudioAsync_NonExistentFile_ThrowsFileNotFoundException() {
        await Assert.ThrowsAsync<FileNotFoundException>(() => _extractor.ExtractAudioAsync("non_existing_file.mp4", "output.wav"));
    }
}