using LocalMediaTranslator.Core.Models;

namespace LocalMediaTranslator.Core.Interfaces;

public interface ISubtitleWriter {
    Task WriteAsyn(SubtitleTrack track, Stream outputStream, SubtitleFormat format = SubtitleFormat.Srt, bool dualLanguage = false, CancellationToken cs = default);
}