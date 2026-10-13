using Callout.Core.Audio;
using NAudio.CoreAudioApi;

namespace Callout.Audio;

internal static class AudioEndpointCatalog
{
    /// <summary>The VB-Cable recording endpoint that receives whatever is played into "CABLE Input".</summary>
    private const string VirtualCableCaptureName = "CABLE Output";

    /// <summary>
    /// Things the app can listen to, best first: the virtual cable (voice chat only), each app playing
    /// audio (one game on its own, no microphone permission), loopback of each playback device, then other inputs.
    /// </summary>
    /// <param name="savedId">Kept in the list when it is an app that is not running, so the choice survives restarts.</param>
    public static IReadOnlyList<AudioSource> ListSources(string? savedId)
    {
        var recording = List(DataFlow.Capture, device => new AudioSource(device.ID, device.FriendlyName, AudioSourceKind.Recording));
        var cables = recording.Where(IsVirtualCable);
        var apps = ListApps(savedId);
        var loopback = List(DataFlow.Render, device =>
            new AudioSource(device.ID, $"Everything playing on {device.FriendlyName}", AudioSourceKind.DeviceLoopback));
        var otherInputs = recording.Where(source => !IsVirtualCable(source))
            .Select(source => source with { DisplayName = $"Input: {source.DisplayName}" });

        return [.. cables, .. apps, .. loopback, .. otherInputs];
    }

    public static IReadOnlyList<AudioEndpoint> ListPlaybackDevices() =>
        List(DataFlow.Render, device => new AudioEndpoint(device.ID, device.FriendlyName));

    /// <summary>Saved choice, else the virtual cable, else everything on the default speakers; never a microphone by default.</summary>
    public static AudioSource? PickSource(IReadOnlyList<AudioSource> sources, string? savedId) =>
        sources.FirstOrDefault(source => source.Id == savedId)
        ?? sources.FirstOrDefault(IsVirtualCable)
        ?? FindDefaultPlaybackDevice(sources.Where(source => source.Kind == AudioSourceKind.DeviceLoopback), source => source.Id)
        ?? sources.FirstOrDefault();

    public static AudioEndpoint? PickPlayback(IReadOnlyList<AudioEndpoint> outputs, string? savedId) =>
        outputs.FirstOrDefault(output => output.Id == savedId)
        ?? FindDefaultPlaybackDevice(outputs, output => output.Id)
        ?? outputs.FirstOrDefault();

    private static List<AudioSource> ListApps(string? savedId)
    {
        var running = AppAudioSessions.ListProcessNames().Select(name => AudioSource.ForApp(name)).ToList();
        if (savedId is not null
            && AudioSource.TryGetAppProcessName(savedId, out var savedProcessName)
            && running.All(app => app.Id != savedId))
        {
            running.Add(AudioSource.ForApp(savedProcessName, isRunning: false));
        }

        return running;
    }

    private static bool IsVirtualCable(AudioSource source) =>
        source.Kind == AudioSourceKind.Recording
        && source.DisplayName.Contains(VirtualCableCaptureName, StringComparison.OrdinalIgnoreCase);

    private static T? FindDefaultPlaybackDevice<T>(IEnumerable<T> candidates, Func<T, string> deviceId)
        where T : class
    {
        using var enumerator = new MMDeviceEnumerator();
        if (!enumerator.TryGetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia, out var defaultDevice))
        {
            return null;
        }

        using (defaultDevice)
        {
            return candidates.FirstOrDefault(candidate => deviceId(candidate) == defaultDevice.ID);
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
