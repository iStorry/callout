using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Callout.Audio;
using Callout.Core.Settings;
using Callout.Core.Speech;
using Callout.Speech;
using Whisper.net.LibraryLoader;

namespace Callout.ViewModels;

public sealed record ModelOption(TranslationModel Model, string Label, string Detail);

public sealed record DisplayModeOption(DisplayMode Mode, string Label);

/// <param name="Code">Whisper language code, or null for per-line auto-detection.</param>
public sealed record LanguageOption(string? Code, string Label);

internal enum ListeningState
{
    Stopped,
    Starting,
    Listening,
}

internal sealed partial class MainViewModel : ObservableObject, IAsyncDisposable
{
    private const long BytesPerMegabyte = 1024 * 1024;
    private const string ReadyStatus = "Ready. Press Start when you're in a match.";
    private static readonly TimeSpan ActivityRefreshInterval = TimeSpan.FromMilliseconds(250);

    private readonly SettingsStore _settingsStore;
    private readonly ModelStore _modelStore;
    private readonly UtteranceSegmenterOptions _segmenterOptions;
    private readonly CancellationTokenSource _lifetime = new();

    // Runs only while listening, so a stopped app does no periodic work.
    private readonly DispatcherTimer _activityTimer = new() { Interval = ActivityRefreshInterval };
    private TranslationSession? _session;

    public MainViewModel(AppSettings settings, SettingsStore settingsStore, ModelStore modelStore, ChatViewModel chat, OverlayViewModel overlay)
    {
        _settingsStore = settingsStore;
        _modelStore = modelStore;
        _segmenterOptions = settings.Segmenter;
        Chat = chat;
        Overlay = overlay;

        PlaybackEnabled = settings.PlaybackEnabled;
        SelectedModel = ModelOptions.First(option => option.Model == settings.Model);
        SelectedLanguage = LanguageOptions.FirstOrDefault(option => option.Code == settings.SpokenLanguage) ?? LanguageOptions[0];
        HideEnglishSpeech = settings.HideEnglishSpeech;
        SelectedDisplayMode = DisplayModeOptions.First(option => option.Mode == settings.DisplayMode);
        LoadDevices(settings.SourceId, settings.PlaybackDeviceId);
        _activityTimer.Tick += (_, _) => Activity = _session?.Monitor.Snapshot();
    }

    public ChatViewModel Chat { get; }

    public OverlayViewModel Overlay { get; }

    public IReadOnlyList<ModelOption> ModelOptions { get; } =
    [
        new(TranslationModel.Small, "Fast", "Lightest on your GPU, misses more slang. ~190 MB download."),
        new(TranslationModel.Medium, "Balanced", "Accurate enough for callouts, light on resources. ~540 MB download."),
        new(TranslationModel.LargeV3, "Best", "Most accurate, needs a strong GPU with 2 GB+ free VRAM. ~1.1 GB download."),
    ];

    public IReadOnlyList<LanguageOption> LanguageOptions { get; } =
    [
        new(null, "Auto-detect, any language"),
        .. SpeechLanguages.Pinnable.Select(language => new LanguageOption(language.Code, language.Name)),
    ];

    public IReadOnlyList<DisplayModeOption> DisplayModeOptions { get; } =
    [
        new(DisplayMode.ChatWindow, "Chat window"),
        new(DisplayMode.Overlay, "Game overlay"),
    ];

    [ObservableProperty]
    public partial IReadOnlyList<AudioSource> Sources { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OffersPlayback), nameof(CanPickPlaybackDevice))]
    public partial AudioSource? SelectedSource { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<AudioEndpoint> PlaybackDevices { get; set; } = [];

    [ObservableProperty]
    public partial AudioEndpoint? SelectedPlaybackDevice { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPickPlaybackDevice))]
    public partial bool PlaybackEnabled { get; set; }

    [ObservableProperty]
    public partial ModelOption SelectedModel { get; set; }

    [ObservableProperty]
    public partial LanguageOption SelectedLanguage { get; set; }

    [ObservableProperty]
    public partial bool HideEnglishSpeech { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChatMode), nameof(IsOverlayMode))]
    public partial DisplayModeOption SelectedDisplayMode { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = ReadyStatus;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditSettings), nameof(CanPickPlaybackDevice), nameof(ToggleLabel), nameof(IsListening))]
    public partial ListeningState State { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InputLevel), nameof(ActivitySummary))]
    public partial SessionActivity? Activity { get; set; }

    public bool IsListening => State == ListeningState.Listening;

    public double InputLevel => Activity?.InputLevel ?? 0;

    /// <summary>Shows each pipeline stage working: audio arriving, lines cut, translated, skipped, or dropped.</summary>
    public string ActivitySummary => Activity is not { } activity
        ? string.Empty
        : string.Join(" · ", DescribeActivity(activity));

    public bool CanEditSettings => State == ListeningState.Stopped;

    /// <summary>
    /// Pass-through is only for recording sources like the virtual cable, which are otherwise silent.
    /// Loopback sources are already audible, and replaying them into the same device would echo forever.
    /// </summary>
    public bool OffersPlayback => SelectedSource is { Kind: AudioSourceKind.Recording };

    public bool CanPickPlaybackDevice => CanEditSettings && PlaybackEnabled;

    public bool IsChatMode => SelectedDisplayMode.Mode == DisplayMode.ChatWindow;

    public bool IsOverlayMode => SelectedDisplayMode.Mode == DisplayMode.Overlay;

    public string ToggleLabel => State switch
    {
        ListeningState.Stopped => "Start listening",
        ListeningState.Starting => "Starting…",
        ListeningState.Listening => "Stop",
        _ => throw new InvalidOperationException($"Unknown state {State}"),
    };

    private ITranscriptSurface ActiveSurface => SelectedDisplayMode.Mode switch
    {
        DisplayMode.ChatWindow => Chat,
        DisplayMode.Overlay => Overlay,
        _ => throw new InvalidOperationException($"Unknown display mode {SelectedDisplayMode.Mode}"),
    };

    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync();
        if (ToggleListeningCommand.ExecutionTask is { } pendingToggle)
        {
            await pendingToggle.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        await StopAsync(ReadyStatus);
        SaveSettings();
        _lifetime.Dispose();
    }

    partial void OnSelectedDisplayModeChanged(DisplayModeOption value)
    {
        if (value.Mode == DisplayMode.ChatWindow)
        {
            Chat.IsOpen = true;
        }
    }

    [RelayCommand]
    private void RefreshDevices() => LoadDevices(SelectedSource?.Id, SelectedPlaybackDevice?.Id);

    [RelayCommand]
    private void OpenChat() => Chat.IsOpen = true;

    [RelayCommand]
    private Task ToggleListeningAsync() => _session is null ? StartAsync() : StopAsync(ReadyStatus);

    private void LoadDevices(string? sourceId, string? playbackId)
    {
        Sources = AudioEndpointCatalog.ListSources();
        PlaybackDevices = AudioEndpointCatalog.ListPlaybackDevices();
        SelectedSource = AudioEndpointCatalog.PickSource(Sources, sourceId);
        SelectedPlaybackDevice = AudioEndpointCatalog.PickPlayback(PlaybackDevices, playbackId);
    }

    private async Task StartAsync()
    {
        if (SelectedSource is not { } source)
        {
            Status = "Pick where Valorant voice chat comes from.";
            return;
        }

        State = ListeningState.Starting;
        try
        {
            SaveSettings();
            var (whisperModelPath, vadModelPath) = await EnsureModelsAsync(SelectedModel.Model);

            Status = "Loading model…";
            var setup = new SessionSetup(
                new TranslatorOptions(whisperModelPath, vadModelPath, SelectedLanguage.Code, HideEnglishSpeech),
                source,
                OffersPlayback && PlaybackEnabled ? SelectedPlaybackDevice?.Id : null,
                _segmenterOptions);
            var session = await TranslationSession.StartAsync(setup, new Progress<TranslatedLine>(ShowLine), _lifetime.Token);

            _session = session;
            State = ListeningState.Listening;
            Status = $"Listening. Translating on {DescribeBackend(SpeechTranslator.Backend)}.";
            _activityTimer.Start();
            _ = StopWhenSessionFailsAsync(session);
        }
        catch (Exception error)
        {
            State = ListeningState.Stopped;
            Status = $"Couldn't start: {error.Message}";
        }
    }

    private async Task<(string WhisperModelPath, string VadModelPath)> EnsureModelsAsync(TranslationModel model)
    {
        var progress = new Progress<DownloadProgress>(download =>
            Status = $"Downloading {download.FileName}: {download.BytesDownloaded / BytesPerMegabyte} MB (first run only)");

        var whisperModel = _modelStore.EnsureWhisperModelAsync(model, progress, _lifetime.Token);
        var vadModel = _modelStore.EnsureVadModelAsync(progress, _lifetime.Token);
        await Task.WhenAll(whisperModel, vadModel);
        return (await whisperModel, await vadModel);
    }

    private void ShowLine(TranslatedLine line) => ActiveSurface.Show(new TranscriptLine(line.Text, line.Language, DateTimeOffset.Now));

    private async Task StopAsync(string status)
    {
        _activityTimer.Stop();
        Activity = null;
        if (_session is { } session)
        {
            _session = null;
            await session.DisposeAsync();
        }

        State = ListeningState.Stopped;
        Status = status;
    }

    private async Task StopWhenSessionFailsAsync(TranslationSession session)
    {
        try
        {
            await session.Completion;
        }
        catch (Exception error)
        {
            // A session that was already stopped has nothing left to report.
            if (_session == session)
            {
                await StopAsync($"Stopped: {error.Message}");
            }
        }
    }

    private void SaveSettings() => _settingsStore.Save(new AppSettings
    {
        SourceId = SelectedSource?.Id,
        PlaybackEnabled = PlaybackEnabled,
        PlaybackDeviceId = SelectedPlaybackDevice?.Id,
        Model = SelectedModel.Model,
        SpokenLanguage = SelectedLanguage.Code,
        HideEnglishSpeech = HideEnglishSpeech,
        DisplayMode = SelectedDisplayMode.Mode,
        ChatWindowBounds = Chat.Bounds,
        ChatAlwaysOnTop = Chat.AlwaysOnTop,
        OverlayLeft = Overlay.Left,
        OverlayTop = Overlay.Top,
        Segmenter = _segmenterOptions,
    });

    private static string DescribeBackend(RuntimeLibrary? backend) => backend switch
    {
        RuntimeLibrary.Vulkan => "your GPU",
        RuntimeLibrary.Cpu or RuntimeLibrary.CpuNoAvx => "the CPU (no compatible GPU found). Use Fast quality to keep up",
        _ => backend?.ToString() ?? "an unknown backend",
    };

    private static IEnumerable<string> DescribeActivity(SessionActivity activity)
    {
        yield return $"Heard {activity.LinesHeard}";
        yield return $"shown {activity.LinesShown}";
        yield return $"skipped {activity.LinesFiltered}";
        if (activity.LinesDropped > 0)
        {
            yield return $"dropped {activity.LinesDropped} (falling behind)";
        }

        if (activity.LinesInProgress > 0)
        {
            yield return $"translating {activity.LinesInProgress}";
        }

        if (activity.LastTranslationTime is { } took)
        {
            yield return $"last line took {took.TotalSeconds:0.0} s";
        }
    }
}
