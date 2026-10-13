namespace Callout.Core.Speech;

public sealed record UtteranceSegmenterOptions
{
    /// <summary>Frame loudness (RMS) at or above which a frame counts as someone talking.</summary>
    public float SpeechRmsThreshold { get; init; } = 0.008f;

    public TimeSpan FrameLength { get; init; } = TimeSpan.FromMilliseconds(20);

    /// <summary>Quiet audio kept before speech starts so the first syllable is not clipped.</summary>
    public TimeSpan PreRoll { get; init; } = TimeSpan.FromMilliseconds(240);

    /// <summary>How long the speaker must stay quiet before the utterance is considered finished.</summary>
    public TimeSpan SilenceToEnd { get; init; } = TimeSpan.FromMilliseconds(700);

    /// <summary>Utterances with less loud audio than this are dropped as clicks or bumps.</summary>
    public TimeSpan MinSpeech { get; init; } = TimeSpan.FromMilliseconds(300);

    /// <summary>Long monologues are cut at this length so translations keep flowing.</summary>
    public TimeSpan MaxUtterance { get; init; } = TimeSpan.FromSeconds(12);
}
