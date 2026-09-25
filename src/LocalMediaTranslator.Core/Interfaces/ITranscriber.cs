using LocalMediaTranslator.Core.Models;

namespace LocalMediaTranslator.Core.Interfaces;

public interface ITranscriber {
    /// <summary>
    /// Transcribes an audio file into timestamped subtitle segments
    /// </summary>
    /// <param name="audioWavPath">Absolute path to the audio file</param>
    /// <param name="options">Transciber options like language and model source</param>
    /// <param name="cs"></param>
    /// <returns></returns>
    IAsyncEnumerable<SubtitleItem> TranscribeAsync(string audioWavPath, TranscriptionOptions options, CancellationToken cs = default);
}