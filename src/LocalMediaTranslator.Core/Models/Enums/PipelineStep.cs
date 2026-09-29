namespace LocalMediaTranslator.Core.Models.Enums;

public enum PipelineStep {
    ExtractingAudio,
    Transcribing,
    Translating,
    WritingSubtitles,
    Completed
}