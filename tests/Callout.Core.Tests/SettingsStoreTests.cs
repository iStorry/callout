using System.Text.Json;
using Callout.Core.Settings;

namespace Callout.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "callout-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Load_WithoutSavedFile_ReturnsDefaults()
    {
        Assert.Equal(new AppSettings(), new SettingsStore(_directory).Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var store = new SettingsStore(_directory);
        var settings = new AppSettings
        {
            SourceId = "{0.0.1.00000000}.{cable}",
            PlaybackEnabled = false,
            Model = TranslationModel.LargeV3,
            SpokenLanguage = "tr",
            HideEnglishSpeech = false,
            DisplayMode = DisplayMode.Overlay,
            ChatWindowBounds = new WindowBounds(100, 80, 420, 600),
            ChatAlwaysOnTop = true,
            OverlayLeft = 40,
            OverlayTop = 120,
            Segmenter = new() { SpeechRmsThreshold = 0.02f },
        };

        store.Save(settings);

        Assert.Equal(settings, store.Load());
    }

    [Fact]
    public void Load_CorruptFile_Throws()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "settings.json"), "{ not json");

        Assert.ThrowsAny<JsonException>(() => new SettingsStore(_directory).Load());
    }
}
