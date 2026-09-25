namespace LocalMediaTranslator.Core.Models;

public class AudioExtrationOptions {
    public int SampleRate { get; set; } = 16000;
    public int Channels { get; set; } = 1;
    public AudioCodec Codec { get; set; } = AudioCodec.pcm_s16le;
}