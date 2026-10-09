using LocalMediaTranslator.Core.Interfaces;
using LocalMediaTranslator.Core.Models;
using LocalMediaTranslator.Core.Models.Enums;
using LocalMediaTranslator.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LocalMediaTranslator.Tests;

public class TranscriberFactoryTests {
    private static TranscriptionOptions Options(string? local = null, string? server = null)
        => new() { LocalModelPath = local, ServerModel = server };

    [Fact]
    public void SelectBackend_LocalModelPathOnly_ReturnsLocal() {
        var backend = TranscriberFactory.SelectBackend(Options(local: "/models/ggml-base.bin"));
        Assert.Equal(TranscriberBackend.Local, backend);
    }

    [Fact]
    public void SelectBackend_ServerModelOnly_ReturnsHttp() {
        var backend = TranscriberFactory.SelectBackend(Options(server: "Systran/faster-whisper-large-v3"));
        Assert.Equal(TranscriberBackend.Http, backend);
    }

    [Fact]
    public void SelectBackend_BothSet_Throws() {
        var ex = Assert.Throws<ArgumentException>(
            () => TranscriberFactory.SelectBackend(Options(local: "/models/ggml-base.bin", server: "large-v3")));
        Assert.Contains("exactly one", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectBackend_NeitherSet_Throws() {
        var ex = Assert.Throws<ArgumentException>(() => TranscriberFactory.SelectBackend(Options()));
        Assert.Contains("Neither", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SelectBackend_WhitespaceLocalPath_TreatedAsUnset(string local) {
        // A blank path is not a usable model, so it must not select the Local backend.
        var backend = TranscriberFactory.SelectBackend(Options(local: local, server: "large-v3"));
        Assert.Equal(TranscriberBackend.Http, backend);
    }

    [Fact]
    public void Create_ResolvesTheKeyedTranscriberForTheSelectedBackend() {
        // Exercises the real DI resolution path: the factory must pull the keyed
        // registration that matches SelectBackend's verdict.
        var services = new ServiceCollection();
        services.AddKeyedSingleton<ITranscriber>(TranscriberBackend.Http, new StubTranscriber("http"));
        services.AddKeyedSingleton<ITranscriber>(TranscriberBackend.Local, new StubTranscriber("local"));
        services.AddSingleton<ITranscriberFactory, TranscriberFactory>();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ITranscriberFactory>();

        var http = factory.Create(Options(server: "large-v3"));
        Assert.Equal(TranscriberBackend.Http, http.Backend);
        Assert.Equal("http", ((StubTranscriber)http.Transcriber).Name);

        var local = factory.Create(Options(local: "/models/ggml-base.bin"));
        Assert.Equal(TranscriberBackend.Local, local.Backend);
        Assert.Equal("local", ((StubTranscriber)local.Transcriber).Name);
    }

    private sealed class StubTranscriber(string name) : ITranscriber {
        public string Name { get; } = name;

        public async IAsyncEnumerable<SubtitleItem> TranscribeAsync(string audioWavPath, TranscriptionOptions options,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cs = default) {
            await Task.CompletedTask;
            yield break;
        }
    }
}
