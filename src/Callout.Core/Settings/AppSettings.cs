using Callout.Core.Speech;

namespace Callout.Core.Settings;

public sealed record AppSettings
{
    /// <summary>What to listen to: an audio device id, or the game's process name for per-app capture.</summary>
    public string? SourceId { get; init; }

    public bool PlaybackEnabled { get; init; } = true;

    public string? PlaybackDeviceId { get; init; }

    public TranslationModel Model { get; init; } = TranslationModel.Medium;

    /// <summary>Whisper code of the language teammates speak, or null to detect it per line.</summary>
    public string? SpokenLanguage { get; init; }

    public bool HideEnglishSpeech { get; init; } = true;

    public DisplayMode DisplayMode { get; init; } = DisplayMode.ChatWindow;

    public WindowBounds? ChatWindowBounds { get; init; }

    public bool ChatAlwaysOnTop { get; init; }

    public double? OverlayLeft { get; init; }

    public double? OverlayTop { get; init; }

    public UtteranceSegmenterOptions Segmenter { get; init; } = new();
}
