namespace Callout.Core.Audio;

/// <summary>A playback device, e.g. the user's headset.</summary>
public sealed record AudioEndpoint(string Id, string DisplayName);

public enum AudioSourceKind
{
    /// <summary>A recording device such as the VB-Cable output or a microphone. Id is the device id.</summary>
    Recording,

    /// <summary>Everything a playback device plays. Id is the device id.</summary>
    DeviceLoopback,

    /// <summary>Only the audio one application plays (Windows per-process loopback). Id encodes the process name.</summary>
    AppLoopback,
}

/// <summary>Something Callout can listen to.</summary>
public sealed record AudioSource(string Id, string DisplayName, AudioSourceKind Kind)
{
    private const string AppIdPrefix = "app:";

    // Unreal Engine games ship as "<Game>-Win64-Shipping.exe"; the suffix is noise in a picker.
    private const string UnrealShippingSuffix = "-Win64-Shipping";

    public static AudioSource ForApp(string processName, bool isRunning = true)
    {
        var name = processName.EndsWith(UnrealShippingSuffix, StringComparison.OrdinalIgnoreCase)
            ? processName[..^UnrealShippingSuffix.Length]
            : processName;
        var displayName = isRunning ? $"App: {name}" : $"App: {name} (not running)";
        return new AudioSource(AppIdPrefix + processName, displayName, AudioSourceKind.AppLoopback);
    }

    public static bool TryGetAppProcessName(string id, out string processName)
    {
        processName = id.StartsWith(AppIdPrefix, StringComparison.Ordinal) ? id[AppIdPrefix.Length..] : string.Empty;
        return processName.Length > 0;
    }
}
