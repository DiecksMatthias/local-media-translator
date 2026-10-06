namespace LocalMediaTranslator.Core.Models;

public class PipelineExecutionOptions {
    public required string MediaFilePath { get; init; }
    public required string OutputSrtPath { get; init; }
    public string? LocalModelPath { get; init; }
    public bool DualLanguage { get; init; }
}