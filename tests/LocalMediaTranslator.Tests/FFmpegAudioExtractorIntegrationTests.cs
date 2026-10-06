using CliWrap;
using LocalMediaTranslator.Core.Models;
using LocalMediaTranslator.Infrastructure.Services;

namespace LocalMediaTranslator.Tests;

public class FFmpegAudioExtractorIntegrationTests : IAsyncLifetime {
    private readonly FFmpegAudioExtractor _extractor = new();
    private readonly string _testDir = Path.Combine(Path.GetTempPath(), "LocalMediaTranslatorTests_" + Guid.NewGuid());
    private string _sampleVideoPath = string.Empty;
    public async Task InitializeAsync() {
        Directory.CreateDirectory(_testDir);
        _sampleVideoPath = Path.Combine(_testDir, "sample.mp4");

        await CliWrap.Cli.Wrap("ffmpeg")
                 .WithArguments(args => args
                    .Add("-y")
                    .Add("-f").Add("lavfi").Add("-i").Add("testsrc=duration=1:size=320x240:rate=30")
                    .Add("-f").Add("lavfi").Add("-i").Add("sine=frequency=1000:duration=1")
                    .Add("-c:v").Add("libx264")
                    .Add("-c:a").Add("aac")
                    .Add(_sampleVideoPath))
                .ExecuteAsync();
    }

    public Task DisposeAsync() {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ExtractAudioAsync_ValidVideo_ExtractsWavFileSuccessfully() {
        var outputPath = Path.Combine(_testDir, "output.wav");
        var options = new AudioExtractionOptions() {
            SampleRate = 16000,
            Channels = 1
        };

        var resultPath = await _extractor.ExtractAudioAsync(_sampleVideoPath, outputPath, options);

        Assert.Equal(outputPath, resultPath);
        Assert.True(File.Exists(outputPath));

        var fileInfo = new FileInfo(outputPath);
        Assert.True(fileInfo.Length > 0, "Extracted WAV file should not be empty");
    }
}