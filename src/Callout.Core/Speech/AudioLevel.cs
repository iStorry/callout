namespace Callout.Core.Speech;

public static class AudioLevel
{
    /// <summary>Quietest level the input meter shows; anything below reads as silence.</summary>
    private const float MeterFloorDecibels = -60f;

    public static float Rms(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty)
        {
            return 0f;
        }

        var sumOfSquares = 0f;
        foreach (var sample in samples)
        {
            sumOfSquares += sample * sample;
        }

        return MathF.Sqrt(sumOfSquares / samples.Length);
    }

    /// <summary>Maps loudness onto 0..1 on a decibel scale, which is how a level meter reads naturally.</summary>
    public static double ToMeter(float rms)
    {
        if (rms <= 0f)
        {
            return 0;
        }

        var decibels = 20f * MathF.Log10(rms);
        return Math.Clamp((decibels - MeterFloorDecibels) / -MeterFloorDecibels, 0f, 1f);
    }
}
