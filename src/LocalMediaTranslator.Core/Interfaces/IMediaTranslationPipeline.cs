using LocalMediaTranslator.Core.Models;

namespace LocalMediaTranslator.Core.Interfaces;

public interface IMediaTranslationPipeline {
    Task<string?> ExecuteAsync(PipelineExecutionOptions options,
                              IProgress<PipelineProgressReport>? progress = null,
                              CancellationToken cs = default);
}