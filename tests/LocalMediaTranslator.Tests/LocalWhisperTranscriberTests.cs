using LocalMediaTranslator.Core.Models;
using LocalMediaTranslator.Infrastructure.Services;

namespace LocalMediaTranslator.Tests;

public class LocalWhisperTranscriberTests {
    private readonly LocalWhisperTranscriber _transcriber = new();

    [Fact]
    public async Task AudioTranscribe_NonExistingAudioFile_ThrowsFileNotFoundException() {
        var options = new TranscriptionOptions { LocalModelPath = "models/tiny.bin" };
        await Assert.ThrowsAsync<FileNotFoundException>(async () => {
            var stream = _transcriber.TranscribeAsync("not_existing_file.wav", options);
            await foreach (var _ in stream) {
                // This should trigger the exception
            }
        });
    }
    [Fact]
    public async Task AudioTranscribe_NonExistingModelFile_ThrowsFileNotFoundException() {
        var tempAudioFile = Path.GetTempFileName();
        try {
            var options = new TranscriptionOptions { LocalModelPath = "non_existing_model.bin" };
            await Assert.ThrowsAsync<FileNotFoundException>(async () => {
                var stream = _transcriber.TranscribeAsync(tempAudioFile, options);
                await foreach (var _ in stream) {
                    // This should trigger the exception                
                }
            });
        }
        finally {
            if (File.Exists(tempAudioFile))
                File.Delete(tempAudioFile);
        }
    }
}