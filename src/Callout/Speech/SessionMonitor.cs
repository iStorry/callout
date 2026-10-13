using Callout.Core.Speech;

namespace Callout.Speech;

/// <param name="InputLevel">Current input loudness on a 0..1 meter scale.</param>
/// <param name="LinesHeard">Utterances cut from the audio.</param>
/// <param name="LinesShown">Utterances that produced a translation.</param>
/// <param name="LinesFiltered">Utterances that turned out to be noise, English, or a Whisper hallucination.</param>
/// <param name="LinesDropped">Utterances discarded unprocessed because translation fell behind.</param>
internal sealed record SessionActivity(
    double InputLevel,
    int LinesHeard,
    int LinesShown,
    int LinesFiltered,
    int LinesDropped,
    TimeSpan? LastTranslationTime)
{
    public int LinesInProgress => LinesHeard - LinesShown - LinesFiltered - LinesDropped;
}

/// <summary>Counts what each pipeline stage did. Written from the capture and translation threads, read by the UI.</summary>
internal sealed class SessionMonitor
{
    private const long NoTranslationYet = -1;

    private float _inputRms;
    private int _linesHeard;
    private int _linesShown;
    private int _linesFiltered;
    private int _linesDropped;
    private long _lastTranslationTicks = NoTranslationYet;

    public void RecordInput(ReadOnlySpan<float> samples) => Volatile.Write(ref _inputRms, AudioLevel.Rms(samples));

    public void RecordHeard() => Interlocked.Increment(ref _linesHeard);

    public void RecordDropped() => Interlocked.Increment(ref _linesDropped);

    public void RecordTranslated(bool shown, TimeSpan took)
    {
        Interlocked.Increment(ref shown ? ref _linesShown : ref _linesFiltered);
        Volatile.Write(ref _lastTranslationTicks, took.Ticks);
    }

    public SessionActivity Snapshot()
    {
        var lastTicks = Volatile.Read(ref _lastTranslationTicks);
        return new SessionActivity(
            AudioLevel.ToMeter(Volatile.Read(ref _inputRms)),
            Volatile.Read(ref _linesHeard),
            Volatile.Read(ref _linesShown),
            Volatile.Read(ref _linesFiltered),
            Volatile.Read(ref _linesDropped),
            lastTicks == NoTranslationYet ? null : TimeSpan.FromTicks(lastTicks));
    }
}
