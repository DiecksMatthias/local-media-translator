using LocalMediaTranslator.Core.Models;

namespace LocalMediaTranslator.Core.Interfaces;

public interface IAudioExtractor {
    /// <summary>
    /// Extracts an audio stream from a video file into a Whisper-compatible WAV file
    /// </summary>
    /// <param name="videoPath">Absolute path to source video</param>
    /// <param name="outputPath">Where the extracted .wav should be saved</param>
    /// <param name="options">Audio sampling and format settings</param>
    /// <param name="cs"></param>
    /// <returns>The path to the generated audio file</returns>
    Task<string> ExtractAudioAsync(string videoPath, string outputPath, AudioExtractionOptions? options = null, CancellationToken cs = default);
}