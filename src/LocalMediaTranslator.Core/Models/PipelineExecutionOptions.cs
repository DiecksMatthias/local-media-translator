namespace LocalMediaTranslator.Core.Models;

public class PipelineExecutionOptions {
    public required string MediaFilePath { get; init; }
    public required string OutputSrtPath { get; init; }
    public string? ModelPath { get; init; }
    public bool DualLanguage { get; init; }
}