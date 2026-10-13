using Callout.Core.Speech;

namespace Callout.Core.Tests;

public class UtteranceSegmenterTests
{
    private const float SpeechAmplitude = 0.2f;

    private static readonly UtteranceSegmenterOptions Options = new();

    private readonly List<float[]> _utterances = [];

    private UtteranceSegmenter CreateSegmenter() => new(Options, _utterances.Add);

    private static float[] Speech(TimeSpan duration) => Tone(duration, SpeechAmplitude);

    private static float[] Silence(TimeSpan duration) => new float[SpeechAudioFormat.SamplesIn(duration)];

    private static float[] Tone(TimeSpan duration, float amplitude)
    {
        var samples = new float[SpeechAudioFormat.SamplesIn(duration)];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = amplitude * MathF.Sin(2 * MathF.PI * 220 * i / SpeechAudioFormat.SampleRate);
        }

        return samples;
    }

    private static TimeSpan Ms(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    [Fact]
    public void Silence_ProducesNothing()
    {
        var segmenter = CreateSegmenter();

        segmenter.Push(Silence(TimeSpan.FromSeconds(5)));
        segmenter.Flush();

        Assert.Empty(_utterances);
    }

    [Fact]
    public void SpeechFollowedBySilence_EmitsOneUtteranceWithPreRollAndTail()
    {
        var segmenter = CreateSegmenter();

        segmenter.Push(Silence(Ms(500)));
        segmenter.Push(Speech(Ms(1000)));
        segmenter.Push(Silence(Ms(1000)));

        var utterance = Assert.Single(_utterances);
        var expected = Options.PreRoll + Ms(1000) + Options.SilenceToEnd;
        Assert.Equal(SpeechAudioFormat.SamplesIn(expected), utterance.Length);
    }

    [Fact]
    public void ShortClick_IsDropped()
    {
        var segmenter = CreateSegmenter();

        segmenter.Push(Speech(Ms(100)));
        segmenter.Push(Silence(Ms(1000)));

        Assert.Empty(_utterances);
    }

    [Fact]
    public void QuietNoise_BelowThreshold_IsIgnored()
    {
        var segmenter = CreateSegmenter();

        segmenter.Push(Tone(TimeSpan.FromSeconds(2), Options.SpeechRmsThreshold / 2));
        segmenter.Flush();

        Assert.Empty(_utterances);
    }

    [Fact]
    public void PauseShorterThanSilenceToEnd_KeepsOneUtterance()
    {
        var segmenter = CreateSegmenter();

        segmenter.Push(Speech(Ms(600)));
        segmenter.Push(Silence(Options.SilenceToEnd - Ms(200)));
        segmenter.Push(Speech(Ms(600)));
        segmenter.Push(Silence(Ms(1000)));

        Assert.Single(_utterances);
    }

    [Fact]
    public void LongMonologue_IsSplitAtMaxUtterance()
    {
        var segmenter = CreateSegmenter();

        segmenter.Push(Speech(Options.MaxUtterance + TimeSpan.FromSeconds(3)));
        segmenter.Push(Silence(Ms(1000)));

        Assert.Equal(2, _utterances.Count);
        Assert.Equal(SpeechAudioFormat.SamplesIn(Options.MaxUtterance), _utterances[0].Length);
    }

    [Fact]
    public void ChunkBoundaries_DoNotChangeTheResult()
    {
        var stream = Silence(Ms(300)).Concat(Speech(Ms(900))).Concat(Silence(Ms(1000))).ToArray();
        var segmenter = CreateSegmenter();

        const int oddChunk = 137;
        for (var offset = 0; offset < stream.Length; offset += oddChunk)
        {
            segmenter.Push(stream.AsSpan(offset, Math.Min(oddChunk, stream.Length - offset)));
        }

        var whole = new List<float[]>();
        new UtteranceSegmenter(Options, whole.Add).Push(stream);

        Assert.Equal(Assert.Single(whole), Assert.Single(_utterances));
    }

    [Fact]
    public void Flush_EmitsUtteranceStillInProgress()
    {
        var segmenter = CreateSegmenter();

        segmenter.Push(Speech(Ms(800)));
        Assert.Empty(_utterances);

        segmenter.Flush();

        Assert.Single(_utterances);
    }
}
