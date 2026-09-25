namespace LocalMediaTranslator.Core.Models;

public class AudioExtrationOptions {
    public int SampleRate { get; set; } = 16000;
    public int Channels { get; set; } = 1;
    public string Codec { get; set; } = "pcm_s16le"; // maybe rather enum?
}