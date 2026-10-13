using LocalMediaTranslator.Cli.Commands;
using LocalMediaTranslator.Cli.Helper;
using LocalMediaTranslator.Core.Interfaces;
using LocalMediaTranslator.Core.Models;
using LocalMediaTranslator.Core.Models.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace LocalMediaTranslator.Tests;

/// <summary>
/// Smoke tests for the CLI command tree.
///
/// These exist because Spectre validates its option definitions when the tree
/// is built, not when a command runs. A duplicated short flag -- e.g. -s on
/// both --server-type and --server-model -- therefore kills EVERY invocation,
/// including --help, while the build stays clean and the unit suite stays green.
/// That happened once; these tests are the guard.
/// </summary>
public class CliCommandRegistrationTests {
    /// <summary>
    /// Minimal stand-in for the real services so the container can resolve a
    /// command's constructor. --help never executes a command body, so these
    /// are never called; they only need to be resolvable.
    /// </summary>
    private sealed class StubPipeline : IMediaTranslationPipeline {
        public Task<string?> ExecuteAsync(PipelineExecutionOptions options, IProgress<PipelineProgressReport>? progress = null, CancellationToken cs = default)
            => Task.FromResult<string?>(options.OutputSrtPath);
    }

    private sealed class StubMediaServerClient : IMediaServerClient {
        public Task<MediaScene?> GetSceneAsync(string sceneId, CancellationToken cs = default)
            => Task.FromResult<MediaScene?>(null);

        public Task<IReadOnlyList<MediaScene>> FindScenesAsync(MediaSceneFilter filter, CancellationToken cs = default)
            => Task.FromResult<IReadOnlyList<MediaScene>>([]);

        public Task<bool> TriggerMetadataScanAsync(IReadOnlyList<string> paths, CancellationToken cs = default)
            => Task.FromResult(true);
    }

    /// <summary>
    /// Builds the same command tree Program.cs builds. Kept deliberately in sync
    /// with Program.cs: if a command is added there and not here, these tests
    /// stop guarding it.
    /// </summary>
    private static CommandApp CreateApp() {
        var services = new ServiceCollection();

        services.AddSingleton<IMediaTranslationPipeline, StubPipeline>();
        services.AddSingleton<IOptions<MediaClientOptions>>(Options.Create(new MediaClientOptions {
            ServerType = MediaServerType.Stash
        }));
        services.AddKeyedSingleton<IMediaServerClient, StubMediaServerClient>(MediaServerType.Stash);

        var app = new CommandApp(new TypeRegistrar(services));

        app.Configure(config => {
            config.ConfigureConsole(new TestConsole());
            config.AddCommand<TranslateFileCommand>("translate");
            config.AddCommand<DiscoverCommand>("discover");
            config.AddCommand<TranslateSceneCommand>("translate-scene");
        });

        return app;
    }

    /// <summary>
    /// Every command's option definitions are validated here, when the tree is
    /// run -- not when it is built. A duplicated short flag (e.g. -s on both
    /// --server-type and --server-model) fails ALL of these, including the root
    /// --help, while the build and the unit suite stay green. That happened
    /// once; this is the guard.
    /// </summary>
    [Theory]
    [InlineData("--help")]
    [InlineData("translate", "--help")]
    [InlineData("translate-scene", "--help")]
    [InlineData("discover", "--help")]
    public async Task Help_RunsWithoutError(params string[] args) {
        var app = CreateApp();

        var exitCode = await app.RunAsync(args);

        Assert.Equal(0, exitCode);
    }

    /// <summary>
    /// GetService returns null for an unregistered type; GetRequiredService
    /// throws. This matters because Spectre renders a null resolve as the
    /// opaque "Could not resolve type 'X'" with NO inner exception at all --
    /// there is nothing for a handler to walk. The resolver must throw so a
    /// real cause always exists to surface.
    /// </summary>
    [Fact]
    public void Resolve_WhenTypeUnregistered_ThrowsInsteadOfReturningNull() {
        var resolver = new TypeResolver(new ServiceCollection().BuildServiceProvider());

        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(typeof(Boom)));
    }

    /// <summary>
    /// A constructor that throws must propagate its own exception, not a
    /// generic resolution failure. The top-level handler walks this chain to
    /// print the root cause.
    /// </summary>
    [Fact]
    public void Resolve_WhenConstructorThrows_PropagatesTheRealException() {
        var services = new ServiceCollection();
        // Factory form, not `new Boom()`: the constructor must only run when
        // the service is resolved, which is the moment under test.
        services.AddSingleton<Boom>(_ => new Boom());

        using var resolver = new TypeResolver(services.BuildServiceProvider());

        var ex = Assert.Throws<InvalidOperationException>(() => resolver.Resolve(typeof(Boom)));

        Assert.Contains("constructor exploded", ex.ToString(), StringComparison.Ordinal);
    }

    private sealed class Boom {
        public Boom() => throw new InvalidOperationException("constructor exploded");
    }
}
