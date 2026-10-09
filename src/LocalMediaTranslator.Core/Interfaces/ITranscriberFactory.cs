using LocalMediaTranslator.Core.Models;
using LocalMediaTranslator.Core.Models.Enums;

namespace LocalMediaTranslator.Core.Interfaces;

public readonly record struct TranscriberSelection(TranscriberBackend Backend, ITranscriber Transcriber);
public interface ITranscriberFactory {
    TranscriberSelection Create(TranscriptionOptions options);
}