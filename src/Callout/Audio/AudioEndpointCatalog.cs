using NAudio.CoreAudioApi;

namespace Callout.Audio;

internal static class AudioEndpointCatalog
{
    /// <summary>The VB-Cable recording endpoint that receives whatever is played into "CABLE Input".</summary>
    private const string VirtualCableCaptureName = "CABLE Output";

    private static readonly AudioSource GameAudio = new(GameProcess.Name, $"{GameProcess.DisplayName} (game audio only)", AudioSourceKind.AppLoopback);

    /// <summary>
    /// Things the app can listen to, best first: the virtual cable (voice chat only), the game itself
    /// (no install, no microphone permission), loopback of each playback device, then other inputs.
    /// </summary>
    public static IReadOnlyList<AudioSource> ListSources()
    {
        var recording = List(DataFlow.Capture, device => new AudioSource(device.ID, device.FriendlyName, AudioSourceKind.Recording));
        var cables = recording.Where(IsVirtualCable);
        var loopback = List(DataFlow.Render, device =>
            new AudioSource(device.ID, $"Everything playing on {device.FriendlyName}", AudioSourceKind.DeviceLoopback));
        var otherInputs = recording.Where(source => !IsVirtualCable(source))
            .Select(source => source with { DisplayName = $"Input: {source.DisplayName}" });

        return [.. cables, GameAudio, .. loopback, .. otherInputs];
    }

    public static IReadOnlyList<AudioEndpoint> ListPlaybackDevices() =>
        List(DataFlow.Render, device => new AudioEndpoint(device.ID, device.FriendlyName));

    /// <summary>Saved choice, else the virtual cable, else the game's own audio; never a microphone by default.</summary>
    public static AudioSource PickSource(IReadOnlyList<AudioSource> sources, string? savedId) =>
        sources.FirstOrDefault(source => source.Id == savedId)
        ?? sources.FirstOrDefault(IsVirtualCable)
        ?? GameAudio;

    public static AudioEndpoint? PickPlayback(IReadOnlyList<AudioEndpoint> outputs, string? savedId) =>
        outputs.FirstOrDefault(output => output.Id == savedId)
        ?? (DefaultPlaybackDeviceId() is { } defaultId ? outputs.FirstOrDefault(output => output.Id == defaultId) : null)
        ?? outputs.FirstOrDefault();

    private static bool IsVirtualCable(AudioSource source) =>
        source.Kind == AudioSourceKind.Recording
        && source.DisplayName.Contains(VirtualCableCaptureName, StringComparison.OrdinalIgnoreCase);

    private static string? DefaultPlaybackDeviceId()
    {
        using var enumerator = new MMDeviceEnumerator();
        if (!enumerator.TryGetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia, out var defaultDevice))
        {
            return null;
        }

        using (defaultDevice)
        {
            return defaultDevice.ID;
        }
    }

    private static List<T> List<T>(DataFlow flow, Func<MMDevice, T> describe)
    {
        using var enumerator = new MMDeviceEnumerator();
        var endpoints = new List<T>();
        foreach (var device in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
        {
            using (device)
            {
                endpoints.Add(describe(device));
            }
        }

        return endpoints;
    }
}
