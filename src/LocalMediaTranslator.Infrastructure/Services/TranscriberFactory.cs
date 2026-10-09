using LocalMediaTranslator.Core.Interfaces;
using LocalMediaTranslator.Core.Models;
using LocalMediaTranslator.Core.Models.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace LocalMediaTranslator.Infrastructure.Services;

public class TranscriberFactory : ITranscriberFactory {
    private readonly IServiceProvider _provider;

    public TranscriberFactory(IServiceProvider provider) {
        _provider = provider;
    }

    public TranscriberSelection Create(TranscriptionOptions options) {
        var backend = SelectBackend(options);
        var transcriber = _provider.GetRequiredKeyedService<ITranscriber>(backend);
        return new TranscriberSelection(backend, transcriber);
    }
    
    internal static TranscriberBackend SelectBackend(TranscriptionOptions options) {
        bool hasLocal = !string.IsNullOrWhiteSpace(options.LocalModelPath);
        bool hasServer = !string.IsNullOrWhiteSpace(options.ServerModel);

        return (hasLocal, hasServer) switch {
            (true, true) => throw new ArgumentException("Both LocalModelPath and ServerModel are set. Set exactly one. "
                + "Use --local-model for local Whisper or --server-model for the remote server"),
            (true, false) => TranscriberBackend.Local,
            (false, true) => TranscriberBackend.Http,
            _ => throw new ArgumentException("Neither LocalModelPath nor ServerModel is set. "
                + "Pass --local-model or --server-model")
        };
    }
}