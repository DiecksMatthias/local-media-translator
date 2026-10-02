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
    public static List<SubtitleItem> MergedAdjacentContext(IReadOnlyList<SubtitleItem> items, double maxGapMs = 400, double maxCombinedSeconds = 6.0) {
        if (items is null || items.Count == 0)
            return [];

        var merged = new List<SubtitleItem>();
        var current = items[0];

        // japanese sentence end markers
        // for now only japanese
        // later TODO maybe add a enum to adjust to other languages if needed
        char[] sentenceTerminators = ['。', '！', '？', '!', '?', '…'];

        for (int i = 1; i < items.Count; i++) {
            var next = items[i];
            var gap = (next.Start - current.End).TotalMilliseconds;
            var totalDuration = (next.End - current.Start).TotalSeconds;

            var endWithPunctuation = current.OriginalText is not null
                                     && sentenceTerminators.Any(t => current.OriginalText.TrimEnd().EndsWith(t));

            // condition: short gap, under max time limit and sentence didn't end
            if (gap <= maxGapMs && totalDuration <= maxCombinedSeconds && !endWithPunctuation) {
                current.End = next.End;
                current.OriginalText = $"{current.OriginalText} {next.OriginalText}".Trim();
            }
            else {
                merged.Add(current);
                current = next;
            }
        }
        merged.Add(current);

        // re-index items
        for (int i = 0; i < merged.Count; i++)
            merged[i].Index = i + 1;

        return merged;
    }
}