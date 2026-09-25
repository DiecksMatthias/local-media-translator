using System.Text;
using LocalMediaTranslator.Core.Models;
using LocalMediaTranslator.Core.Models.Enums;
using LocalMediaTranslator.Infrastructure.Services;

namespace LocalMediaTranslator.Tests;

public class SrtSubtitleWriterTests {
    private readonly SrtSubtitleWriter _writer = new();

    [Fact]
    public async Task WriteAsync_SingleLanguage_FormatCorrectSrt() {
        // Arrange
        var track = new SubtitleTrack {
            SourceFileName = "test.mp4",
            Language = LanguageCode.Japanese,
            Items = new List<SubtitleItem> {
                new() {
                    Index = 1,
                    Start = TimeSpan.FromSeconds(1.5),
                    End = TimeSpan.FromSeconds(4.25),
                    OriginalText = "こんにちは"
                }
            }
        };
        using var ms = new MemoryStream();

        // Act
        await _writer.WriteAsync(track, ms, SubtitleFormat.Srt, dualLanguage: false);
        var output = Encoding.UTF8.GetString(ms.ToArray());

        // Assert
        var expected = "1\n00:00:01,500 --> 00:00:04,250\nこんにちは\n\n";
        Assert.Equal(expected, output);
    }

    [Fact]
    public async Task WriteAsync_DualLanguage_StacksOriginalAndTranslated() {
        // Arrange
        var track = new SubtitleTrack {
            SourceFileName = "test.mp4",
            Language = LanguageCode.Japanese,
            TargetedLanguage = LanguageCode.English,
            Items = new List<SubtitleItem> {
                new() {
                    Index = 1,
                    Start = TimeSpan.Zero,
                    End = TimeSpan.FromSeconds(2),
                    OriginalText = "ありがとう",
                    TranslatedText = "Thank you"
                }
            }
        };
        using var ms = new MemoryStream();

        // Act
        await _writer.WriteAsync(track, ms, SubtitleFormat.Srt, dualLanguage: true);
        var output = Encoding.UTF8.GetString(ms.ToArray());

        // Assert
        var expected = "1\n00:00:00,000 --> 00:00:02,000\nありがとう\nThank you\n\n";
        Assert.Equal(expected, output);
    }

    [Fact]
    public async Task WriteAsync_UnsupportedFormat_ThrowsNotSupportedException() {
        // Arrange
        var track = new SubtitleTrack {
            SourceFileName = "test.mp4",
            Language = LanguageCode.Japanese
        };
        using var ms = new MemoryStream();

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() => _writer.WriteAsync(track, ms, SubtitleFormat.Vtt));
    }
}