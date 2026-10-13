using System.Diagnostics;
using System.Threading.Channels;
using Callout.Audio;
using Callout.Core.Speech;

namespace Callout.Speech;

internal sealed record SessionSetup(
    TranslatorOptions Translator,
    AudioSource Source,
    string? PlaybackDeviceId,
    UtteranceSegmenterOptions Segmenter);

/// <summary>
/// One listening run: capture → segment → translate → report lines.
/// Owns the loaded models, so stopping frees all RAM and VRAM they used.
/// </summary>
internal sealed class TranslationSession : IAsyncDisposable
{
    /// <summary>When the GPU falls behind, old callouts are dropped: a late translation is worthless mid-round.</summary>
    private const int MaxQueuedUtterances = 3;

    private static readonly BoundedChannelOptions QueueOptions = new(MaxQueuedUtterances)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
        SingleWriter = true,
    };

    private readonly CancellationTokenSource _stopping = new();
    private readonly Channel<float[]> _utterances;
    private readonly VoiceChatCapture _capture;
    private readonly SpeechTranslator _translator;

    private TranslationSession(
        Channel<float[]> utterances,
        SessionMonitor monitor,
        VoiceChatCapture capture,
        SpeechTranslator translator,
        IProgress<TranslatedLine> translatedLines)
    {
        _utterances = utterances;
        Monitor = monitor;
        _capture = capture;
        _translator = translator;
        _capture.Faulted += error => _utterances.Writer.TryComplete(error);
        Completion = Task.Run(() => TranslateUtterancesAsync(translatedLines));
    }

    /// <summary>Completes when the session is stopped; faults if capture or translation fails.</summary>
    public Task Completion { get; }

    public SessionMonitor Monitor { get; }

    /// <remarks>Opens the audio source before loading the model, so a missing game or device fails in a moment, not seconds.</remarks>
    public static async Task<TranslationSession> StartAsync(
        SessionSetup setup,
        IProgress<TranslatedLine> translatedLines,
        CancellationToken cancellationToken)
    {
        var monitor = new SessionMonitor();
        var utterances = Channel.CreateBounded<float[]>(QueueOptions, _ => monitor.RecordDropped());
        var segmenter = new UtteranceSegmenter(setup.Segmenter, utterance =>
        {
            monitor.RecordHeard();
            utterances.Writer.TryWrite(utterance);
        });
        var capture = await VoiceChatCapture.CreateAsync(setup.Source, setup.PlaybackDeviceId, samples =>
        {
            monitor.RecordInput(samples);
            segmenter.Push(samples);
        });

        SpeechTranslator translator;
        try
        {
            translator = await Task.Run(() => SpeechTranslator.Load(setup.Translator, setup.Segmenter.MaxUtterance), cancellationToken);
        }
        catch
        {
            capture.Dispose();
            throw;
        }

        var session = new TranslationSession(utterances, monitor, capture, translator, translatedLines);
        try
        {
            capture.Start();
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }

        return session;
    }

    public async ValueTask DisposeAsync()
    {
        _capture.Dispose();
        _utterances.Writer.TryComplete();
        await _stopping.CancelAsync();

        // Failures are reported to the owner through Completion; disposing only waits for shutdown.
        await Completion.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        await _translator.DisposeAsync();
        _stopping.Dispose();
    }

    private async Task TranslateUtterancesAsync(IProgress<TranslatedLine> translatedLines)
    {
        try
        {
            await foreach (var utterance in _utterances.Reader.ReadAllAsync(_stopping.Token))
            {
                var started = Stopwatch.GetTimestamp();
                var line = await _translator.TranslateAsync(utterance, _stopping.Token);
                Monitor.RecordTranslated(shown: line is not null, Stopwatch.GetElapsedTime(started));
                if (line is not null)
                {
                    translatedLines.Report(line);
                }
            }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
            // Stopped by the user mid-translation.
        }
    }
}
