using Callout.Core.Speech;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Callout.Audio;

internal delegate void SpeechSamplesHandler(ReadOnlySpan<float> samples);

/// <summary>
/// Captures the chosen source (virtual cable, the game's own audio, or a whole playback device),
/// optionally plays it back to the user's headset (a virtual cable is otherwise silent),
/// and delivers 16 kHz mono samples for recognition.
/// </summary>
internal sealed class VoiceChatCapture : IDisposable
{
    private static readonly TimeSpan SpeechBufferDuration = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PlaybackBufferDuration = TimeSpan.FromMilliseconds(500);
    private const int PlaybackLatencyMilliseconds = 60;
    private static readonly TimeSpan SpeechReadChunk = TimeSpan.FromMilliseconds(100);

    /// <summary>Per-app capture has no device mix format to inherit, so it must ask for one; the pipeline resamples anyway.</summary>
    private static readonly WaveFormat AppLoopbackFormat = new(44_100, 16, 2);

    /// <summary>Windows 10 version 2004, the first build with per-process loopback capture.</summary>
    private const int AppLoopbackMinimumBuild = 19041;

    private readonly MMDevice? _sourceDevice;
    private readonly WasapiRecorder _recorder;
    private readonly BufferedWaveProvider _speechBuffer;
    private readonly ISampleProvider _speechPipeline;
    private readonly float[] _speechSamples = new float[SpeechAudioFormat.SamplesIn(SpeechReadChunk)];
    private readonly SpeechSamplesHandler _onSpeechSamples;
    private readonly Playback? _playback;
    private byte[] _silence = [];

    private VoiceChatCapture(MMDevice? sourceDevice, WasapiRecorder recorder, Playback? playback, SpeechSamplesHandler onSpeechSamples)
    {
        _sourceDevice = sourceDevice;
        _recorder = recorder;
        _playback = playback;
        _onSpeechSamples = onSpeechSamples;

        _speechBuffer = new BufferedWaveProvider(recorder.WaveFormat.AsStandardWaveFormat(), SpeechBufferDuration)
        {
            DiscardOnBufferOverflow = true,
            ReadFully = false,
        };
        _speechPipeline = BuildSpeechPipeline(_speechBuffer);

        _recorder.DataAvailable += OnDataAvailable;
        _recorder.RecordingStopped += OnRecordingStopped;
    }

    /// <exception cref="GameNotRunningException">The source is the game's own audio and the game is not running.</exception>
    public static async Task<VoiceChatCapture> CreateAsync(AudioSource source, string? playbackDeviceId, SpeechSamplesHandler onSpeechSamples)
    {
        using var enumerator = new MMDeviceEnumerator();
        MMDevice? sourceDevice = null;
        WasapiRecorder? recorder = null;
        Playback? playback = null;
        try
        {
            var builder = new WasapiRecorderBuilder().WithSharedMode();
            switch (source.Kind)
            {
                case AudioSourceKind.Recording:
                    sourceDevice = enumerator.GetDevice(source.Id);
                    builder.WithDevice(sourceDevice);
                    break;
                case AudioSourceKind.DeviceLoopback:
                    sourceDevice = enumerator.GetDevice(source.Id);
                    builder.WithDevice(sourceDevice).WithLoopbackCapture();
                    break;
                case AudioSourceKind.AppLoopback:
                    if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, AppLoopbackMinimumBuild))
                    {
                        throw new PlatformNotSupportedException(
                            "Capturing a single app needs Windows 10 version 2004 or newer. Pick \"Everything playing on…\" or VB-Cable instead.");
                    }

                    builder.WithProcessLoopback(GameProcess.FindId(source.Id), ProcessLoopbackMode.IncludeTargetProcessTree)
                        .WithFormat(AppLoopbackFormat);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(source), source.Kind, null);
            }

            recorder = await builder.BuildAsync();
            playback = playbackDeviceId is null
                ? null
                : Playback.Create(enumerator.GetDevice(playbackDeviceId), recorder.WaveFormat.AsStandardWaveFormat());
            return new VoiceChatCapture(sourceDevice, recorder, playback, onSpeechSamples);
        }
        catch
        {
            playback?.Dispose();
            recorder?.Dispose();
            sourceDevice?.Dispose();
            throw;
        }
    }

    /// <summary>Raised on the capture thread when the device fails (unplugged, disabled, taken exclusively).</summary>
    public event Action<Exception>? Faulted;

    public void Start()
    {
        _playback?.Start();
        _recorder.StartRecording();
    }

    public void Dispose()
    {
        _recorder.DataAvailable -= OnDataAvailable;
        _recorder.RecordingStopped -= OnRecordingStopped;
        _recorder.Dispose();
        _sourceDevice?.Dispose();
        _playback?.Dispose();
    }

    private static ISampleProvider BuildSpeechPipeline(IWaveProvider source)
    {
        var samples = source.ToSampleProvider();
        var mono = samples.WaveFormat.Channels == 1 ? samples : new DownmixToMonoSampleProvider(samples);
        return new WdlResamplingSampleProvider(mono, SpeechAudioFormat.SampleRate);
    }

    private void OnDataAvailable(ReadOnlySpan<byte> buffer, AudioClientBufferFlags flags, long devicePosition, long qpcPosition)
    {
        // WASAPI may hand over garbage with the Silent flag; the contract is to treat it as zeros.
        var audio = flags.HasFlag(AudioClientBufferFlags.Silent) ? Silence(buffer.Length) : buffer;
        _playback?.Add(audio);
        _speechBuffer.AddSamples(audio);

        int read;
        while ((read = _speechPipeline.Read(_speechSamples)) > 0)
        {
            _onSpeechSamples(_speechSamples.AsSpan(0, read));
        }
    }

    private ReadOnlySpan<byte> Silence(int length)
    {
        if (_silence.Length < length)
        {
            _silence = new byte[length];
        }

        return _silence.AsSpan(0, length);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            Faulted?.Invoke(e.Exception);
        }
    }

    private sealed class Playback : IDisposable
    {
        private readonly MMDevice _device;
        private readonly BufferedWaveProvider _buffer;
        private readonly WasapiPlayer _output;

        private Playback(MMDevice device, BufferedWaveProvider buffer, WasapiPlayer output)
        {
            _device = device;
            _buffer = buffer;
            _output = output;
        }

        /// <summary>Takes ownership of <paramref name="device"/>, releasing it if the output cannot be opened.</summary>
        public static Playback Create(MMDevice device, WaveFormat format)
        {
            WasapiPlayer? output = null;
            try
            {
                var buffer = new BufferedWaveProvider(format, PlaybackBufferDuration) { DiscardOnBufferOverflow = true };
                output = new WasapiPlayerBuilder().WithDevice(device).WithSharedMode().WithLatency(PlaybackLatencyMilliseconds).Build();
                output.Init(buffer);
                return new Playback(device, buffer, output);
            }
            catch
            {
                output?.Dispose();
                device.Dispose();
                throw;
            }
        }

        public void Start() => _output.Play();

        public void Add(ReadOnlySpan<byte> audio) => _buffer.AddSamples(audio);

        public void Dispose()
        {
            _output.Dispose();
            _device.Dispose();
        }
    }
}
