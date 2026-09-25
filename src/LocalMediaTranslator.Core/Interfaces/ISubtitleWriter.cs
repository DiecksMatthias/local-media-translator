using LocalMediaTranslator.Core.Models;
using LocalMediaTranslator.Core.Models.Enums;

namespace LocalMediaTranslator.Core.Interfaces;

public interface ISubtitleWriter {
    Task WriteAsyn(SubtitleTrack track, Stream outputStream, SubtitleFormat format = SubtitleFormat.Srt, bool dualLanguage = false, CancellationToken cs = default);
}