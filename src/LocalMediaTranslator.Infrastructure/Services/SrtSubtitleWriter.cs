using System.Text;
using LocalMediaTranslator.Core.Interfaces;
using LocalMediaTranslator.Core.Models;
using LocalMediaTranslator.Core.Models.Enums;

namespace LocalMediaTranslator.Infrastructure.Services;

public class SrtSubtitleWriter : ISubtitleWriter {
    public async Task WriteAsync(SubtitleTrack track, Stream outputStream, SubtitleFormat format = SubtitleFormat.Srt, bool dualLanguage = false, CancellationToken cs = default) {
        // Guards
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(outputStream);
        if (!outputStream.CanWrite)
            throw new ArgumentException(message: "Output stream must be writable");
        if (format != SubtitleFormat.Srt)
            throw new NotSupportedException(message: "Wrong Format. Only Srt supported");

        using StreamWriter sw = new StreamWriter(outputStream, encoding: Encoding.UTF8, leaveOpen: true);
        foreach (var subtitleItem in track.Items) {
            await sw.WriteAsync(CreateSrtSegment(subtitleItem, dualLanguage).AsMemory(), cs);
        }
        await sw.FlushAsync(cs);
    }

    private static string TimeSpanToValidSrtTime(TimeSpan span) => span.ToString(@"hh\:mm\:ss\,fff");
    private string CreateSrtSegment(SubtitleItem item, bool dualLanguage) =>
        $"{item.Index}\n{TimeSpanToValidSrtTime(item.Start)} --> {TimeSpanToValidSrtTime(item.End)}\n{item.GetFormattedText(dualLanguage)}\n\n";
}