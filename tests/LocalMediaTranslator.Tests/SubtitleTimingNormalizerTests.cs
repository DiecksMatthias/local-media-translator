using LocalMediaTranslator.Core.Models;
using LocalMediaTranslator.Core.Models.Enums;
using LocalMediaTranslator.Core.Utilities;

namespace LocalMediaTranslator.Tests;

public class SubtitleTimingNormalizerTests {
    [Fact]
    public void NormalizeTimestamps_NullInput_DoesNotThrow() {
        var exception = Record.Exception(() => SubtitleTimingNormalizer.NormalizeTimestamps(null));
        Assert.Null(exception);
    }

    [Fact]
    public void NormalizeTimestamps_EmptyItems_DoesNotThrowAndLeavesListEmpty() {
        var track = new SubtitleTrack {
            SourceFileName = "test.mp4",
            Language = LanguageCode.Japanese,
            Items = new List<SubtitleItem>()
        };

        SubtitleTimingNormalizer.NormalizeTimestamps(track);

        Assert.Empty(track.Items);
    }

    [Fact]
    public void NormalizeTimestamps_ShortDuration_PadsToMinimumDuration() {
        var track = new SubtitleTrack {
            SourceFileName = "test.mp4",
            Language = LanguageCode.Japanese,
            Items = new List<SubtitleItem> {
                new() {
                    Index = 1,
                    Start = TimeSpan.FromSeconds(2.0),
                    End = TimeSpan.FromSeconds(2.5), // 0.5s duration
                    OriginalText = "はい",
                    TranslatedText = "Yes"
                }
            }
        };

        SubtitleTimingNormalizer.NormalizeTimestamps(track);

        var item = track.Items[0];
        Assert.Equal(TimeSpan.FromSeconds(2.0), item.Start);
        // Duration should be at least 1.5s -> End should be at 3.5s
        Assert.Equal(TimeSpan.FromSeconds(3.5), item.End);
    }

    [Fact]
    public void NormalizeTimestamps_ExcessivelyLongSegment_ClampsToIdealDuration() {
        var track = new SubtitleTrack {
            SourceFileName = "test.mp4",
            Language = LanguageCode.Japanese,
            Items = new List<SubtitleItem> {
                new() {
                    Index = 1,
                    Start = TimeSpan.FromSeconds(10.0),
                    End = TimeSpan.FromSeconds(25.0), // 15s duration (too long)
                    OriginalText = "おはよう",
                    TranslatedText = "Good morning" // 12 chars -> 12 * 0.08 = 0.96s -> clamped to min 1.5s
                }
            }
        };

        SubtitleTimingNormalizer.NormalizeTimestamps(track);

        var item = track.Items[0];
        Assert.Equal(TimeSpan.FromSeconds(10.0), item.Start);
        Assert.Equal(TimeSpan.FromSeconds(11.5), item.End); // 10.0 + 1.5s
    }

    [Fact]
    public void NormalizeTimestamps_OverlappingAdjacentSegments_Inserts50MsGap() {
        var track = new SubtitleTrack {
            SourceFileName = "test.mp4",
            Language = LanguageCode.Japanese,
            Items = new List<SubtitleItem> {
                new() {
                    Index = 1,
                    Start = TimeSpan.FromSeconds(1.0),
                    End = TimeSpan.FromSeconds(5.0),
                    OriginalText = "長いセリフです",
                    TranslatedText = "This is a longer line of dialogue to read."
                },
                new() {
                    Index = 2,
                    Start = TimeSpan.FromSeconds(4.0), // Starts before item 1 ends
                    End = TimeSpan.FromSeconds(6.0),
                    OriginalText = "次",
                    TranslatedText = "Next"
                }
            }
        };

        SubtitleTimingNormalizer.NormalizeTimestamps(track);

        var first = track.Items[0];
        var second = track.Items[1];

        // First item should be clamped to next.Start - 50ms = 3.95s
        Assert.Equal(TimeSpan.FromSeconds(3.95), first.End);
        Assert.True(first.End < second.Start);
    }

    [Fact]
    public void NormalizeTimestamps_FallsBackToOriginalTextWhenTranslationNull() {
        var track = new SubtitleTrack {
            SourceFileName = "test.mp4",
            Language = LanguageCode.Japanese,
            Items = new List<SubtitleItem> {
                new() {
                    Index = 1,
                    Start = TimeSpan.FromSeconds(0),
                    End = TimeSpan.FromSeconds(10.0),
                    OriginalText = "こんにちは",
                    TranslatedText = null
                }
            }
        };

        SubtitleTimingNormalizer.NormalizeTimestamps(track);

        var item = track.Items[0];
        Assert.Equal(TimeSpan.FromSeconds(1.5), item.End); // 5 chars -> clamped to min 1.5s
    }

    [Fact]
    public void MergedAdjacentContext_NullOrEmpty_ReturnsEmptyList() {
        Assert.Empty(SubtitleTimingNormalizer.MergedAdjacentContext(null!));
        Assert.Empty(SubtitleTimingNormalizer.MergedAdjacentContext([]));
    }

    [Fact]
    public void MergedAdjacentContext_ShortGapAndNoPunctuation_MergesAndReindexes() {
        var items = new List<SubtitleItem> {
            new() { Index = 1, Start = TimeSpan.FromSeconds(1.0), End = TimeSpan.FromSeconds(2.0), OriginalText = "俺さあ" },
            new() { Index = 2, Start = TimeSpan.FromSeconds(2.2), End = TimeSpan.FromSeconds(3.5), OriginalText = "北海道に" }, // gap = 200ms -> merged
            new() { Index = 3, Start = TimeSpan.FromSeconds(5.0), End = TimeSpan.FromSeconds(6.0), OriginalText = "行くんだ" }  // gap = 1500ms -> not merged
        };

        var result = SubtitleTimingNormalizer.MergedAdjacentContext(items);

        Assert.Equal(2, result.Count);

        Assert.Equal(1, result[0].Index);
        Assert.Equal("俺さあ 北海道に", result[0].OriginalText);
        Assert.Equal(TimeSpan.FromSeconds(1.0), result[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(3.5), result[0].End);

        Assert.Equal(2, result[1].Index);
        Assert.Equal("行くんだ", result[1].OriginalText);
        Assert.Equal(TimeSpan.FromSeconds(5.0), result[1].Start);
        Assert.Equal(TimeSpan.FromSeconds(6.0), result[1].End);
    }

    [Fact]
    public void MergedAdjacentContext_SentenceTerminators_DoesNotMerge() {
        var items = new List<SubtitleItem> {
            new() { Index = 1, Start = TimeSpan.FromSeconds(1.0), End = TimeSpan.FromSeconds(2.0), OriginalText = "こんにちは。" },
            new() { Index = 2, Start = TimeSpan.FromSeconds(2.1), End = TimeSpan.FromSeconds(3.0), OriginalText = "元気ですか？" },
            new() { Index = 3, Start = TimeSpan.FromSeconds(3.1), End = TimeSpan.FromSeconds(4.0), OriginalText = "はい！" },
            new() { Index = 4, Start = TimeSpan.FromSeconds(4.1), End = TimeSpan.FromSeconds(5.0), OriginalText = "そう…" },
            new() { Index = 5, Start = TimeSpan.FromSeconds(5.1), End = TimeSpan.FromSeconds(6.0), OriginalText = "終わり!" }
        };

        var result = SubtitleTimingNormalizer.MergedAdjacentContext(items);

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void MergedAdjacentContext_ExceedingMaxCombinedDuration_DoesNotMerge() {
        var items = new List<SubtitleItem> {
            new() { Index = 1, Start = TimeSpan.FromSeconds(0.0), End = TimeSpan.FromSeconds(4.0), OriginalText = "長いセリフの前半部分で" },
            new() { Index = 2, Start = TimeSpan.FromSeconds(4.1), End = TimeSpan.FromSeconds(7.0), OriginalText = "後半部分もかなり長いです" } // total = 7.0s > 6.0s
        };

        var result = SubtitleTimingNormalizer.MergedAdjacentContext(items, maxCombinedSeconds: 6.0);

        Assert.Equal(2, result.Count);
    }
}
