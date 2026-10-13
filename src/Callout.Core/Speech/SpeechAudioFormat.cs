namespace Callout.Core.Speech;

/// <summary>The audio shape Whisper expects: 16 kHz mono float samples in [-1, 1].</summary>
public static class SpeechAudioFormat
{
    public const int SampleRate = 16_000;

    public static int SamplesIn(TimeSpan duration) => (int)(duration.TotalSeconds * SampleRate);
}
