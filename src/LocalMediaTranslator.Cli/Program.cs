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
services.AddHttpClient<ITranslator, LlmTranslator>((sp, client) => {
    var options = sp.GetRequiredService<IOptions<TranslationOptions>>().Value;
    if (options.EndPoint is not null)
        client.BaseAddress = options.EndPoint;
    if (!string.IsNullOrWhiteSpace(options.ApiKey))
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
    client.Timeout = TimeSpan.FromMinutes(5);
});

services.AddSingleton<IAudioExtractor, FFmpegAudioExtractor>();
services.AddKeyedSingleton<IMediaServerClient, GraphQlMediaClient>(MediaServerType.Stash);
services.AddSingleton<ISubtitleWriter, SrtSubtitleWriter>();
services.AddSingleton<ITranscriber, LocalWhisperTranscriber>();
services.AddSingleton(sp => sp.GetRequiredService<IOptions<MediaClientOptions>>().Value);

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
});
return await app.RunAsync(args);