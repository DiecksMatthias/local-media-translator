using LocalMediaTranslator.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LocalMediaTranslator.Tests;

public class SetCommandTests {
    private static readonly SetCommand _command = new();

    // ── ParseKey ──────────────────────────────────────────────────────────────

    [Fact]
    public void ParseKey_TwoParts_ReturnsNullDictKey() {
        var result = SetCommand.ParseKey(["Transcription", "ServerModel"]);

        Assert.Equal("Transcription", result.Section);
        Assert.Equal("ServerModel", result.Property);
        Assert.Null(result.DictKey);
    }

    [Fact]
    public void ParseKey_ThreeParts_ReturnsDictKey() {
        var result = SetCommand.ParseKey(["MediaServer", "PathMappings", "/data"]);

        Assert.Equal("MediaServer", result.Section);
        Assert.Equal("PathMappings", result.Property);
        Assert.Equal("/data", result.DictKey);
    }

    [Fact]
    public void ParseKey_FourParts_UsesThirdAsDictKey() {
        var result = SetCommand.ParseKey(["A", "B", "C", "D"]);

        Assert.Equal("A", result.Section);
        Assert.Equal("B", result.Property);
        Assert.Equal("C", result.DictKey);
    }

    // ── ValidateSectionAndProperty ─────────────────────────────────────────────

    [Theory]
    [InlineData("Transcription", "ServerModel", null)]
    [InlineData("Transcription", "LocalModelPath", null)]
    [InlineData("Transcription", "Endpoint", null)]
    [InlineData("Transcription", "Language", null)]
    [InlineData("Transcription", "Temperature", null)]
    [InlineData("Translation", "Endpoint", null)]
    [InlineData("Translation", "ApiKey", null)]
    [InlineData("Translation", "Timeout", null)]
    [InlineData("MediaServer", "ServerType", null)]
    [InlineData("MediaServer", "Endpoint", null)]
    [InlineData("MediaServer", "ApiKey", null)]
    [InlineData("MediaServer", "PathMappings", "/data")]
    public void ValidateSectionAndProperty_ValidKeys_DoesNotThrow(string section, string property, string? dictKey) {
        var key = new SetCommand.ParsedKey(section, property, dictKey);

        var ex = Record.Exception(() => _command.ValidateSectionAndProperty(key));

        Assert.Null(ex);
    }

    [Fact]
    public void ValidateSectionAndProperty_UnknownSection_Throws() {
        var key = new SetCommand.ParsedKey("BadSection", "Foo", null);

        var ex = Assert.Throws<InvalidOperationException>(() => _command.ValidateSectionAndProperty(key));

        Assert.Contains("Unknown Section", ex.Message);
        Assert.Contains("BadSection", ex.Message);
    }

    [Fact]
    public void ValidateSectionAndProperty_UnknownProperty_Throws() {
        var key = new SetCommand.ParsedKey("Transcription", "BadKey", null);

        var ex = Assert.Throws<InvalidOperationException>(() => _command.ValidateSectionAndProperty(key));

        Assert.Contains("Unknown key", ex.Message);
        Assert.Contains("Transcription:BadKey", ex.Message);
    }

    [Fact]
    public void ValidateSectionAndProperty_CaseInsensitiveSection_DoesNotThrow() {
        var key = new SetCommand.ParsedKey("transcription", "ServerModel", null);

        var ex = Record.Exception(() => _command.ValidateSectionAndProperty(key));

        Assert.Null(ex);
    }

    // ── WriteConfig ────────────────────────────────────────────────────────────

    [Fact]
    public void WriteConfig_NewFile_SimpleProperty_CreatesFile() {
        var file = Path.Combine(Path.GetTempPath(), $"settest_{Guid.NewGuid()}.json");
        try {
            var key = new SetCommand.ParsedKey("Transcription", "ServerModel", null);

            SetCommand.WriteConfig(file, key, "large-v3");

            var json = File.ReadAllText(file);
            using var doc = JsonDocument.Parse(json);
            Assert.Equal("large-v3", doc.RootElement.GetProperty("Transcription").GetProperty("ServerModel").GetString());
        } finally {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    [Fact]
    public void WriteConfig_NewFile_DictionaryProperty_CreatesNestedObject() {
        var file = Path.Combine(Path.GetTempPath(), $"settest_{Guid.NewGuid()}.json");
        try {
            var key = new SetCommand.ParsedKey("MediaServer", "PathMappings", "/data");

            SetCommand.WriteConfig(file, key, "/mnt/external-hdd/stash-data");

            var json = File.ReadAllText(file);
            using var doc = JsonDocument.Parse(json);
            var mapped = doc.RootElement.GetProperty("MediaServer").GetProperty("PathMappings").GetProperty("/data").GetString();
            Assert.Equal("/mnt/external-hdd/stash-data", mapped);
        } finally {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    [Fact]
    public void WriteConfig_ExistingFile_OverwritesValue() {
        var file = Path.Combine(Path.GetTempPath(), $"settest_{Guid.NewGuid()}.json");
        try {
            File.WriteAllText(file, """{"Transcription":{"ServerModel":"small"}}""");
            var key = new SetCommand.ParsedKey("Transcription", "ServerModel", null);

            SetCommand.WriteConfig(file, key, "large-v3");

            var json = File.ReadAllText(file);
            using var doc = JsonDocument.Parse(json);
            Assert.Equal("large-v3", doc.RootElement.GetProperty("Transcription").GetProperty("ServerModel").GetString());
        } finally {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    [Fact]
    public void WriteConfig_ExistingFile_PreservesOtherSections() {
        var file = Path.Combine(Path.GetTempPath(), $"settest_{Guid.NewGuid()}.json");
        try {
            File.WriteAllText(file, """{"Translation":{"Endpoint":"http://localhost:11434/"}}""");
            var key = new SetCommand.ParsedKey("Transcription", "ServerModel", null);

            SetCommand.WriteConfig(file, key, "large-v3");

            var json = File.ReadAllText(file);
            using var doc = JsonDocument.Parse(json);
            Assert.Equal("http://localhost:11434/", doc.RootElement.GetProperty("Translation").GetProperty("Endpoint").GetString());
            Assert.Equal("large-v3", doc.RootElement.GetProperty("Transcription").GetProperty("ServerModel").GetString());
        } finally {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    [Fact]
    public void WriteConfig_ExistingDictionary_AddsNewKey() {
        var file = Path.Combine(Path.GetTempPath(), $"settest_{Guid.NewGuid()}.json");
        try {
            File.WriteAllText(file, """{"MediaServer":{"PathMappings":{"/data":"/mnt/external-hdd/stash-data"}}}""");
            var key = new SetCommand.ParsedKey("MediaServer", "PathMappings", "/downloads");

            SetCommand.WriteConfig(file, key, "/mnt/external-hdd/stash-downloads");

            var json = File.ReadAllText(file);
            using var doc = JsonDocument.Parse(json);
            var mappings = doc.RootElement.GetProperty("MediaServer").GetProperty("PathMappings");
            Assert.Equal("/mnt/external-hdd/stash-data", mappings.GetProperty("/data").GetString());
            Assert.Equal("/mnt/external-hdd/stash-downloads", mappings.GetProperty("/downloads").GetString());
        } finally {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    [Fact]
    public void WriteConfig_ExistingDictionary_OverwritesExistingKey() {
        var file = Path.Combine(Path.GetTempPath(), $"settest_{Guid.NewGuid()}.json");
        try {
            File.WriteAllText(file, """{"MediaServer":{"PathMappings":{"/data":"/old/path"}}}""");
            var key = new SetCommand.ParsedKey("MediaServer", "PathMappings", "/data");

            SetCommand.WriteConfig(file, key, "/new/path");

            var json = File.ReadAllText(file);
            using var doc = JsonDocument.Parse(json);
            Assert.Equal("/new/path", doc.RootElement.GetProperty("MediaServer").GetProperty("PathMappings").GetProperty("/data").GetString());
        } finally {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    // ── Integration: CommandApp ────────────────────────────────────────────────

    [Fact]
    public async Task SetCommand_ValidSimpleKey_ReturnsZero() {
        var app = CreateAppWithSet();
        var exitCode = await app.RunAsync(["set", "Transcription:ServerModel", "large-v3"]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task SetCommand_InvalidSection_ReturnsOne() {
        var app = CreateAppWithSet();
        var exitCode = await app.RunAsync(["set", "BadSection:Foo", "bar"]);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task SetCommand_InvalidKey_ReturnsOne() {
        var app = CreateAppWithSet();
        var exitCode = await app.RunAsync(["set", "Transcription:BadKey", "bar"]);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task SetCommand_EmptyKey_ReturnsOne() {
        var app = CreateAppWithSet();
        var exitCode = await app.RunAsync(["set", "", "bar"]);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task SetCommand_EmptyValue_ReturnsOne() {
        var app = CreateAppWithSet();
        var exitCode = await app.RunAsync(["set", "Transcription:ServerModel", ""]);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task SetCommand_TooFewParts_ReturnsOne() {
        var app = CreateAppWithSet();
        var exitCode = await app.RunAsync(["set", "JustOnePart", "bar"]);

        Assert.Equal(1, exitCode);
    }

    private static CommandApp CreateAppWithSet() {
        var services = new ServiceCollection();
        services.AddSingleton<LocalMediaTranslator.Core.Interfaces.IMediaTranslationPipeline, StubPipeline>();
        services.AddSingleton<Microsoft.Extensions.Options.IOptions<LocalMediaTranslator.Core.Models.MediaClientOptions>>(
            Microsoft.Extensions.Options.Options.Create(new LocalMediaTranslator.Core.Models.MediaClientOptions {
                ServerType = LocalMediaTranslator.Core.Models.Enums.MediaServerType.Stash
            }));
        services.AddKeyedSingleton<LocalMediaTranslator.Core.Interfaces.IMediaServerClient, StubMediaServerClient>(LocalMediaTranslator.Core.Models.Enums.MediaServerType.Stash);

        var app = new CommandApp(new LocalMediaTranslator.Cli.Helper.TypeRegistrar(services));
        app.Configure(config => {
            config.ConfigureConsole(new TestConsole());
            config.AddCommand<LocalMediaTranslator.Cli.Commands.TranslateFileCommand>("translate");
            config.AddCommand<LocalMediaTranslator.Cli.Commands.DiscoverCommand>("discover");
            config.AddCommand<LocalMediaTranslator.Cli.Commands.TranslateSceneCommand>("translate-scene");
            config.AddCommand<LocalMediaTranslator.Cli.Commands.SetCommand>("set");
        });
        return app;
    }

    private sealed class StubPipeline : LocalMediaTranslator.Core.Interfaces.IMediaTranslationPipeline {
        public Task<string?> ExecuteAsync(
            LocalMediaTranslator.Core.Models.PipelineExecutionOptions options,
            IProgress<LocalMediaTranslator.Core.Models.PipelineProgressReport>? progress = null,
            CancellationToken cs = default)
            => Task.FromResult<string?>(options.OutputSrtPath);
    }

    private sealed class StubMediaServerClient : LocalMediaTranslator.Core.Interfaces.IMediaServerClient {
        public Task<LocalMediaTranslator.Core.Models.MediaScene?> GetSceneAsync(string sceneId, CancellationToken cs = default)
            => Task.FromResult<LocalMediaTranslator.Core.Models.MediaScene?>(null);

        public Task<IReadOnlyList<LocalMediaTranslator.Core.Models.MediaScene>> FindScenesAsync(
            LocalMediaTranslator.Core.Models.MediaSceneFilter filter, CancellationToken cs = default)
            => Task.FromResult<IReadOnlyList<LocalMediaTranslator.Core.Models.MediaScene>>([]);

        public Task<bool> TriggerMetadataScanAsync(IReadOnlyList<string> paths, CancellationToken cs = default)
            => Task.FromResult(true);
    }
}
