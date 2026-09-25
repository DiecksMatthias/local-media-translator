using CliWrap;
using LocalMediaTranslator.Core.Models;
using LocalMediaTranslator.Infrastructure.Services;
using Whisper.net.Ggml;

namespace LocalMediaTranslator.Tests;

public class LocalWhisperTranscriberIntegrationTests : IAsyncLifetime {
    private readonly LocalWhisperTranscriber _transcriber = new();
    private readonly string _testDir = Path.Combine(Path.GetTempPath(), "WhisperTests_" + Guid.NewGuid());
    private string _modelPath = string.Empty;
    private string _sampleWavePath = string.Empty;

    public async Task InitializeAsync() {
        Directory.CreateDirectory(_testDir);
        _modelPath = Path.Combine(_testDir, "ggml-tiny.bin");
        _sampleWavePath = Path.Combine(_testDir, "sample.wav");

        if (!File.Exists(_modelPath)) {
            await using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(GgmlType.Tiny);
            await using var fileStream = File.Create(_modelPath);
            await modelStream.CopyToAsync(fileStream);
        }

        await Cli.Wrap("ffmpeg").WithArguments(args =>
                                    args.Add("-y")
                                    .Add("-f").Add("lavfi").Add("-i").Add("sine=frequency=1000:duration=2")
                                    .Add("-ar").Add(16000)
                                    .Add("-ac").Add(1)
                                    .Add("-c:a").Add("pcm_s16le")
                                    .Add(_sampleWavePath))
                                .ExecuteAsync();
    }

    [Fact]
    public async Task TranscribeAsync_ValidAudioModel_RunsInferenceSuccessfully() {
        var options = new TranscriptionOptions { ModelPath = _modelPath, Language = "auto" };

        var results = new List<SubtitleItem>();
        await foreach (var item in _transcriber.TranscribeAsync(_sampleWavePath, options)) {
            results.Add(item);
        }

        Assert.NotNull(results);
    }

    public Task DisposeAsync() {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
        return Task.CompletedTask;
    }
}