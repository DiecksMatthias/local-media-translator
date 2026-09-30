using Spectre.Console.Cli;
using LocalMediaTranslator.Cli.Commands;
using LocalMediaTranslator.Cli.Helper;
using LocalMediaTranslator.Core.Interfaces;
using LocalMediaTranslator.Core.Models;
using LocalMediaTranslator.Infrastructure.Services;
using LocalMediaTranslator.Core.Models.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddUserSecrets<Program>(optional: true)
    .Build();
var services = new ServiceCollection();

// dependency injection
services.AddHttpClient();
services.AddSingleton<IConfiguration>(configuration);
services.Configure<MediaClientOptions>(configuration.GetSection("MediaServer"));
services.Configure<TranslationOptions>(configuration.GetSection("Translation"));
services.Configure<TranscriptionOptions>(configuration.GetSection("Transcription"));
services.AddHttpClient<ITranslator, LlmTranslator>((sp, client) => {
    var options = sp.GetRequiredService<IOptions<TranslationOptions>>().Value;
    if (options.Endpoint is not null)
        client.BaseAddress = options.Endpoint;
    if (!string.IsNullOrWhiteSpace(options.ApiKey))
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
    client.Timeout = options.Timeout;
});
services.AddHttpClient<ITranscriber, HttpWhisperTranscriber>((sp, client) => {
    var options = sp.GetRequiredService<IOptions<TranscriptionOptions>>().Value;
    if (options.Endpoint is not null)
        client.BaseAddress = options.Endpoint;
    client.Timeout = options.Timeout;
});

services.AddSingleton<IAudioExtractor, FFmpegAudioExtractor>();
services.AddKeyedSingleton<IMediaServerClient, GraphQlMediaClient>(MediaServerType.Stash);
services.AddSingleton<ISubtitleWriter, SrtSubtitleWriter>();
services.AddSingleton(sp => sp.GetRequiredService<IOptions<MediaClientOptions>>().Value);
services.AddSingleton<IMediaTranslationPipeline, MediaTranslationPipeline>();

// wrapping microsoft di into adapter for spectre
var registrar = new TypeRegistrar(services);

var app = new CommandApp(registrar);
app.Configure(config => {
    config.AddCommand<TranslateFileCommand>("translate")
        .WithDescription("Translates a single video file on disk")
        .WithExample(["translate", "video.mp4", "--dual-language"]);
    config.AddCommand<DiscoverCommand>("discover")
        .WithDescription("Queries media servers for scene matching criteria")
        .WithExample(["discover", "--tag", "NeedSubtitles"]);
    config.AddCommand<TranslateSceneCommand>("translate-scene")
        .WithDescription("Translates a scene by ID from the media server and optionally triggers a rescan")
        .WithExample(["translate-scene", "12345", "--dual-language", "--rescan"]);
});
return await app.RunAsync(args);