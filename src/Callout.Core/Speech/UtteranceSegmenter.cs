namespace Callout.Core.Speech;

/// <summary>
/// Splits a continuous 16 kHz mono stream into utterances using frame loudness.
/// The voice-chat channel is clean (only teammates' voices), so loudness is a reliable
/// first cut; the translator double-checks each utterance with a neural VAD.
/// </summary>
public sealed class UtteranceSegmenter
{
    private readonly Action<float[]> _onUtterance;
    private readonly float _speechRmsThreshold;
    private readonly int _preRollFrames;
    private readonly int _silenceFramesToEnd;
    private readonly int _minSpeechFrames;
    private readonly int _maxUtteranceSamples;

    private readonly float[] _frame;
    private int _frameFill;
    private readonly Queue<float[]> _preRoll = new();
    private readonly List<float> _utterance = [];
    private bool _inUtterance;
    private int _speechFrames;
    private int _trailingSilentFrames;

    public UtteranceSegmenter(UtteranceSegmenterOptions options, Action<float[]> onUtterance)
    {
        _onUtterance = onUtterance;
        _speechRmsThreshold = options.SpeechRmsThreshold;

        var frameSamples = SpeechAudioFormat.SamplesIn(options.FrameLength);
        if (frameSamples <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Frame length must be positive.");
        }

        _frame = new float[frameSamples];
        _preRollFrames = FramesIn(options.PreRoll);
        _silenceFramesToEnd = Math.Max(1, FramesIn(options.SilenceToEnd));
        _minSpeechFrames = FramesIn(options.MinSpeech);
        _maxUtteranceSamples = SpeechAudioFormat.SamplesIn(options.MaxUtterance);
    }

    public void Push(ReadOnlySpan<float> samples)
    {
        while (!samples.IsEmpty)
        {
            var take = Math.Min(_frame.Length - _frameFill, samples.Length);
            samples[..take].CopyTo(_frame.AsSpan(_frameFill));
            _frameFill += take;
            samples = samples[take..];

            if (_frameFill == _frame.Length)
            {
                ProcessFrame(_frame);
                _frameFill = 0;
            }
        }
    }

    /// <summary>Emits whatever is in progress, e.g. when capture stops mid-sentence.</summary>
    public void Flush()
    {
        if (_inUtterance)
        {
            FinishUtterance();
        }
    }

    private void ProcessFrame(float[] frame)
    {
        var voiced = AudioLevel.Rms(frame) >= _speechRmsThreshold;

        if (!_inUtterance)
        {
            if (!voiced)
            {
                RememberForPreRoll(frame);
                return;
            }

            BeginUtterance();
        }

        _utterance.AddRange(frame);

        if (voiced)
        {
            _speechFrames++;
            _trailingSilentFrames = 0;
        }
        else
        {
            _trailingSilentFrames++;
        }

        if (_trailingSilentFrames >= _silenceFramesToEnd || _utterance.Count >= _maxUtteranceSamples)
        {
            FinishUtterance();
        }
    }

    private void BeginUtterance()
    {
        _inUtterance = true;
        foreach (var preRollFrame in _preRoll)
        {
            _utterance.AddRange(preRollFrame);
        }

        _preRoll.Clear();
    }

    private void FinishUtterance()
    {
        if (_speechFrames >= _minSpeechFrames)
        {
            _onUtterance(_utterance.ToArray());
        }

        _utterance.Clear();
        _inUtterance = false;
        _speechFrames = 0;
        _trailingSilentFrames = 0;
    }

    private void RememberForPreRoll(float[] frame)
    {
        if (_preRollFrames == 0)
        {
            return;
        }

        var slot = _preRoll.Count == _preRollFrames ? _preRoll.Dequeue() : new float[frame.Length];
        frame.CopyTo(slot, 0);
        _preRoll.Enqueue(slot);
    }

    private int FramesIn(TimeSpan duration) => SpeechAudioFormat.SamplesIn(duration) / _frame.Length;
}
