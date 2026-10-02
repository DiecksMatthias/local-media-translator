using LocalMediaTranslator.Core.Models;

namespace LocalMediaTranslator.Core.Utilities;

public static class SubtitleTimingNormalizer {
    public static void NormalizeTimestamps(SubtitleTrack? input) {
        if (input is null || input.Items.Count == 0)
            return;

        var minDuration = TimeSpan.FromSeconds(1.5);
        var maxDuration = TimeSpan.FromSeconds(6.5);

        for (int i = 0; i < input.Items.Count; i++) {
            var currentSegment = input.Items[i];
            if (currentSegment is null)
                continue;
            var text = !string.IsNullOrWhiteSpace(currentSegment.TranslatedText)
                ? currentSegment.TranslatedText
                : currentSegment.OriginalText;

            // calculate reading speed for english words
            var textDurationSecond = Math.Clamp(text.Length * 0.08, minDuration.TotalSeconds, maxDuration.TotalSeconds);
            var idealDuration = TimeSpan.FromSeconds(textDurationSecond);

            // if the duration for the segment is to long (ie. because of long silence before the next segment)
            // -> shorten it to the ideal duration
            if (currentSegment.Duration > idealDuration)
                currentSegment.End = currentSegment.Start + idealDuration;

            // ensure min duration
            if (currentSegment.Duration < minDuration)
                currentSegment.End = currentSegment.Start + minDuration;

            // check and fix overlapping segments
            if (i < input.Items.Count - 1) {
                var nextSegment = input.Items[i + 1];
                if (currentSegment.End >= nextSegment.Start) {
                    var safeEnd = nextSegment.Start - TimeSpan.FromMilliseconds(50);
                    currentSegment.End = safeEnd > currentSegment.Start ? safeEnd : nextSegment.Start;
                }
            }
        }
        return;
    }
}