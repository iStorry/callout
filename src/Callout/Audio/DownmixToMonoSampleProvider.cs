using NAudio.Wave;

namespace Callout.Audio;

/// <summary>Averages any number of interleaved channels into one (NAudio's ToMono only handles stereo).</summary>
internal sealed class DownmixToMonoSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _channels;
    private float[] _sourceBuffer = [];

    public DownmixToMonoSampleProvider(ISampleProvider source)
    {
        _source = source;
        _channels = source.WaveFormat.Channels;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(Span<float> buffer)
    {
        var needed = buffer.Length * _channels;
        if (_sourceBuffer.Length < needed)
        {
            _sourceBuffer = new float[needed];
        }

        var read = _source.Read(_sourceBuffer.AsSpan(0, needed));
        var frames = read / _channels;

        for (var frame = 0; frame < frames; frame++)
        {
            var sum = 0f;
            var offset = frame * _channels;
            for (var channel = 0; channel < _channels; channel++)
            {
                sum += _sourceBuffer[offset + channel];
            }

            buffer[frame] = sum / _channels;
        }

        return frames;
    }
}
