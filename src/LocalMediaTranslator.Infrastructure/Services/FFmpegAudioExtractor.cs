using LocalMediaTranslator.Core.Interfaces;
using LocalMediaTranslator.Core.Models;
using CliWrap;
using System.Text;
using CliWrap.Builders;

namespace LocalMediaTranslator.Infrastructure.Services;

public class FFmpegAudioExtractor : IAudioExtractor {
    public async Task<string> ExtractAudioAsync(string videoPath,
                                          string outputPath,
                                          AudioExtractionOptions? options = null,
                                          CancellationToken cs = default) {
        // Guards
        if (!File.Exists(videoPath))
            throw new FileNotFoundException(message: $"{videoPath} is not a valid video path");


        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDir) && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);
        options ??= new AudioExtractionOptions();

        var stdErrBuffer = new StringBuilder();
        try {
            var result = await Cli.Wrap("ffmpeg")
                            .WithArguments(args => {
                                args.Add("-y")
                                    .Add("-i").Add(videoPath)
                                    .Add("-vn");

                                // check for flag and add parameters for higher quality audio output
                                // to increase whisperer transcribing quality
                                if (options.EnableVoiceFilter)
                                    args.Add("-af").Add("highpass=f=150,lowpass=f=4000,afftdn=nf=-20");

                                args.Add("-ar").Add(options.SampleRate)
                                    .Add("-ac").Add(options.Channels)
                                    .Add("-c:a").Add(options.Codec)
                                    .Add(outputPath);
                            })
                            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer))
                            .ExecuteAsync(cs);
        }
        catch (Exception e) {
            throw new InvalidOperationException($"FFmpeg failed to extract audio from '{videoPath}'. Output:\n{stdErrBuffer}", e);
        }
        if (!File.Exists(outputPath))
            throw new FileNotFoundException($"Extraction finished but output file was not found: {outputPath}");
        return outputPath;
    }
}