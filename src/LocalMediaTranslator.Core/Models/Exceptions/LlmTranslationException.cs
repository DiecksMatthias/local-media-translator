using LocalMediaTranslator.Core.Models.Enums;

namespace LocalMediaTranslator.Core.Models.Exceptions;

public class LlmTranslationException : Exception {
    public int FailedBatches { get; }
    public int TotalBatches { get; }
    public int FailedItems { get; }
    public LlmTranslationFailureExceptionReasons Reason { get; }

    public LlmTranslationException(int failedBatches, int totalBatches, int failedItems, LlmTranslationFailureExceptionReasons reason, string message, Exception? inner = null) : base(message: message, innerException: inner) {
        FailedBatches = failedBatches;
        TotalBatches = totalBatches;
        FailedItems = failedItems;
        Reason = reason;
    }
}