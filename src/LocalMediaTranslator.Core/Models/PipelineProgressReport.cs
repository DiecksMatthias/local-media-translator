using LocalMediaTranslator.Core.Models.Enums;

namespace LocalMediaTranslator.Core.Models;

public record PipelineProgressReport(
    PipelineStep Step,
    int? ProcessedItems = null,
    int? TotalItems = null,
    double? Percentage = null,
    string? Message = null);