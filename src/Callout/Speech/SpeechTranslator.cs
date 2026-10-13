using System.Text;
using Callout.Core.Speech;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace Callout.Speech;

/// <param name="SpokenLanguage">Whisper language code, or null to detect the language of every line separately.</param>
/// <param name="HideEnglishSpeech">Drop lines that were spoken in English, since they need no translation.</param>
internal sealed record TranslatorOptions(string WhisperModelPath, string VadModelPath, string? SpokenLanguage, bool HideEnglishSpeech);

/// <param name="Language">Whisper code of the language the line was spoken in.</param>
internal sealed record TranslatedLine(string Text, string Language);

/// <summary>Speech in any language Whisper knows in, English text out. Not thread-safe: one utterance at a time.</summary>
internal sealed class SpeechTranslator : IAsyncDisposable
{
    /// <summary>On a GPU the CPU threads only feed it; a couple keeps the game's cores free.</summary>
    private const int GpuFeederThreads = 2;

    // Whisper's encoder always runs over a 30 s window (1500 frames at 50 frames/s) unless told otherwise.
    private const int EncoderFramesPerSecond = 50;
    private const int FullAudioContext = 1500;
    private static readonly TimeSpan AudioContextHeadroom = TimeSpan.FromSeconds(1);

    /// <summary>whisper.cpp silently returns nothing for input shorter than one second, and callouts often are.</summary>
    private static readonly int MinimumInputSamples = SpeechAudioFormat.SamplesIn(TimeSpan.FromMilliseconds(1100));

    private readonly bool _hideEnglishSpeech;
    private readonly WhisperFactory _whisperFactory;
    private readonly WhisperProcessor _whisper;
    private readonly WhisperVadFactory _vadFactory;
    private readonly WhisperVadProcessor _vad;

    static SpeechTranslator()
    {
        // Vulkan runs on NVIDIA, AMD and Intel GPUs with just the graphics driver; CPU is the fallback.
        RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu];
    }

    private SpeechTranslator(TranslatorOptions options, TimeSpan longestUtterance)
    {
        _hideEnglishSpeech = options.HideEnglishSpeech;
        _whisperFactory = WhisperFactory.FromPath(options.WhisperModelPath, new WhisperFactoryOptions { UseFlashAttention = true });

        var builder = _whisperFactory.CreateBuilder();
        if (options.SpokenLanguage is { } language)
        {
            builder.WithLanguage(language);
        }
        else
        {
            // Detection runs per utterance, so players speaking different languages are each handled.
            builder.WithLanguageDetection();
        }

        _whisper = builder
            .WithTranslate()
            .WithNoContext()
            .WithSingleSegment()
            .WithAudioContextSize(AudioContextFor(longestUtterance))
            .WithThreads(ThreadsFor(Backend))
            .Build();

        try
        {
            _vadFactory = WhisperVadFactory.FromPath(options.VadModelPath);
            _vad = _vadFactory.CreateBuilder().WithUseGpu(false).Build();
        }
        catch
        {
            _whisper.Dispose();
            _whisperFactory.Dispose();
            throw;
        }
    }

    /// <summary>Which native backend whisper.cpp loaded, known once a model has been loaded.</summary>
    public static RuntimeLibrary? Backend => RuntimeOptions.LoadedLibrary;

    /// <summary>Loads both models; blocks for several seconds, so call it off the UI thread.</summary>
    /// <param name="longestUtterance">Upper bound on input length, used to shrink the encoder window.</param>
    public static SpeechTranslator Load(TranslatorOptions options, TimeSpan longestUtterance) => new(options, longestUtterance);

    /// <returns>The English translation, or null when there was no real speech or nothing to translate.</returns>
    public async Task<TranslatedLine?> TranslateAsync(float[] utterance, CancellationToken cancellationToken)
    {
        var speech = await _vad.DetectSpeechAsync(utterance, cancellationToken);
        if (speech.Count == 0)
        {
            return null;
        }

        var text = new StringBuilder();
        string? language = null;
        await foreach (var segment in _whisper.ProcessAsync(PadToMinimumLength(utterance), cancellationToken))
        {
            text.Append(segment.Text);
            language ??= segment.Language;
        }

        if (language is null || (_hideEnglishSpeech && language == SpeechLanguages.EnglishCode))
        {
            return null;
        }

        return TranscriptFilter.Clean(text.ToString()) is { } cleaned ? new TranslatedLine(cleaned, language) : null;
    }

    public async ValueTask DisposeAsync()
    {
        await _whisper.DisposeAsync();
        _whisperFactory.Dispose();
        await _vad.DisposeAsync();
        _vadFactory.Dispose();
    }

    /// <summary>
    /// Encoding only as much audio as an utterance can hold instead of the full 30 s
    /// cuts GPU time per line several-fold; utterances are capped well below 30 s.
    /// </summary>
    private static int AudioContextFor(TimeSpan longestUtterance)
    {
        var frames = (int)Math.Ceiling((longestUtterance + AudioContextHeadroom).TotalSeconds * EncoderFramesPerSecond);
        return Math.Min(frames, FullAudioContext);
    }

    /// <summary>
    /// Without a GPU the CPU does all the work, and too few threads make each line take minutes.
    /// Half the cores keeps it usable while leaving the other half to the game.
    /// </summary>
    private static int ThreadsFor(RuntimeLibrary? backend) => backend is RuntimeLibrary.Vulkan
        ? GpuFeederThreads
        : Math.Max(GpuFeederThreads, Environment.ProcessorCount / 2);

    private static float[] PadToMinimumLength(float[] samples)
    {
        if (samples.Length >= MinimumInputSamples)
        {
            return samples;
        }

        var padded = new float[MinimumInputSamples];
        samples.CopyTo(padded, 0);
        return padded;
    }
}
