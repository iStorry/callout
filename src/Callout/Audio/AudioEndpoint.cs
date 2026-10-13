namespace Callout.Audio;

/// <summary>A playback device, e.g. the user's headset.</summary>
public sealed record AudioEndpoint(string Id, string DisplayName);

public enum AudioSourceKind
{
    /// <summary>A recording device such as the VB-Cable output or a microphone. Id is the device id.</summary>
    Recording,

    /// <summary>Everything a playback device plays. Id is the device id.</summary>
    DeviceLoopback,

    /// <summary>Only the audio one application plays (Windows per-process loopback). Id is the process name.</summary>
    AppLoopback,
}

/// <summary>Something Callout can listen to.</summary>
public sealed record AudioSource(string Id, string DisplayName, AudioSourceKind Kind);
