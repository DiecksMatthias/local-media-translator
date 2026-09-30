using LocalMediaTranslator.Core.Models;

namespace LocalMediaTranslator.Core.Interfaces;

public interface ITranslator {
    /// <summary>
    /// Translates the OriginalText of each SubtitleItem and populates TranslatedText
    /// </summary>
    Task<IReadOnlyList<SubtitleItem>> TranslateAsync(IReadOnlyList<SubtitleItem> items, TranslationOptions options, IProgress<int>? progress = null, CancellationToken cs = default);
    Task UnloadAsync(CancellationToken cs = default) => Task.CompletedTask;
}