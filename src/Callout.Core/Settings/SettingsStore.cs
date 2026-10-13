using System.Text.Json;
using System.Text.Json.Serialization;

namespace Callout.Core.Settings;

public sealed class SettingsStore(string directory)
{
    private const string FileName = "settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private string FilePath => Path.Combine(directory, FileName);

    /// <summary>Returns defaults when no settings were saved yet; throws <see cref="JsonException"/> if the file is corrupt.</summary>
    public AppSettings Load()
    {
        if (!File.Exists(FilePath))
        {
            return new AppSettings();
        }

        using var stream = File.OpenRead(FilePath);
        return JsonSerializer.Deserialize<AppSettings>(stream, JsonOptions) ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(directory);
        var pendingPath = FilePath + ".tmp";
        File.WriteAllText(pendingPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(pendingPath, FilePath, overwrite: true);
    }
}
