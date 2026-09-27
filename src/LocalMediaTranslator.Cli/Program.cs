using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using LocalMediaTranslator.Cli.Commands;
using LocalMediaTranslator.Cli.Helper;
using LocalMediaTranslator.Core.Interfaces;
using LocalMediaTranslator.Core.Models;
using LocalMediaTranslator.Infrastructure.Services;

var services = new ServiceCollection();

// dependency injection
services.AddHttpClient();
services.AddSingleton<IAudioExtractor, FFmpegAudioExtractor>();
services.AddSingleton<IMediaServerClient, GraphQlMediaClient>();
services.AddSingleton<ISubtitleWriter, SrtSubtitleWriter>();
services.AddSingleton<ITranscriber, LocalWhisperTranscriber>();
services.AddSingleton<ITranslator, LlmTranslator>();
services.AddSingleton(new MediaClientOptions());

// wrapping microsoft di into adapter for spectre
var registrar = new TypeRegistrar(services);

var app = new CommandApp(registrar);
app.Configure(config => {
    config.AddCommand<TranslateFileCommand>("translate")
        .WithDescription("Translates a single video file on disk")
        .WithExample(["translate", "video.mp4", "--dual-language"]);
});
return await app.RunAsync(args);