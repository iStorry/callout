using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Callout.Core.Settings;
using Callout.Speech;
using Callout.ViewModels;
using Callout.Views;

namespace Callout;

public partial class App : Application
{
    // Left edge, a little below the minimap, clear of the kill feed and scoreboard.
    private const double DefaultOverlayLeft = 24;
    private const double DefaultOverlayTopFraction = 0.4;

    private MainViewModel? _mainViewModel;
    private DisplaySurfaces? _surfaces;
    private bool _shutdownComplete;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // The game always wins a contention for CPU; audio capture keeps its own real-time (MMCSS) priority.
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;

        var dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.Name);
        var settingsStore = new SettingsStore(dataDirectory);
        var settings = LoadSettings(settingsStore);

        var chat = new ChatViewModel(VisibleOrNull(settings.ChatWindowBounds), settings.ChatAlwaysOnTop);
        var overlay = CreateOverlay(settings);
        var modelStore = new ModelStore(Path.Combine(dataDirectory, "models"));
        _mainViewModel = new MainViewModel(settings, settingsStore, modelStore, chat, overlay);

        var mainWindow = new MainWindow(_mainViewModel);
        mainWindow.Closing += OnMainWindowClosing;
        MainWindow = mainWindow;
        mainWindow.Show();

        _surfaces = new DisplaySurfaces(_mainViewModel);
    }

    private static OverlayViewModel CreateOverlay(AppSettings settings) =>
        settings is { OverlayLeft: { } left, OverlayTop: { } top } && ScreenArea.IsVisible(left, top)
            ? new OverlayViewModel(left, top)
            : new OverlayViewModel(DefaultOverlayLeft, SystemParameters.PrimaryScreenHeight * DefaultOverlayTopFraction);

    private static WindowBounds? VisibleOrNull(WindowBounds? bounds) =>
        bounds is not null && ScreenArea.IsVisible(bounds.Left, bounds.Top) ? bounds : null;

    private static AppSettings LoadSettings(SettingsStore store)
    {
        try
        {
            return store.Load();
        }
        catch (Exception error) when (error is JsonException or IOException)
        {
            MessageBox.Show($"Saved settings could not be read and were reset.\n\n{error.Message}", AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
            return new AppSettings();
        }
    }

    /// <summary>Closing waits for audio and the model to be released, then exits for real.</summary>
    private async void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_shutdownComplete || _mainViewModel is null || sender is not Window mainWindow)
        {
            return;
        }

        e.Cancel = true;
        mainWindow.IsEnabled = false;
        try
        {
            await _mainViewModel.DisposeAsync();
        }
        finally
        {
            _shutdownComplete = true;
            _surfaces?.CloseAll();
            Shutdown();
        }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) =>
        MessageBox.Show(e.Exception.ToString(), $"{AppInfo.Name} crashed", MessageBoxButton.OK, MessageBoxImage.Error);
}
