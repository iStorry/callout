using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace Callout.Audio;

internal sealed class AppNotRunningException(string processName)
    : Exception($"{processName} isn't running. Start it, then press Start listening.");

/// <summary>Finds the apps that are playing audio right now, so any game can be captured on its own.</summary>
internal static class AppAudioSessions
{
    private sealed record AudioApp(uint ProcessId, string ProcessName);

    /// <summary>Process names of apps with an audio session on any playback device, excluding Callout itself.</summary>
    public static IReadOnlyList<string> ListProcessNames() =>
        FindAudioApps()
            .Select(app => app.ProcessName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Prefers the process that owns the audio session, since browsers and launchers run many processes with one name.</summary>
    /// <exception cref="AppNotRunningException">No process with that name is running.</exception>
    public static uint FindProcessId(string processName)
    {
        if (FindAudioApps().FirstOrDefault(app => app.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase)) is { } audioApp)
        {
            return audioApp.ProcessId;
        }

        var processes = Process.GetProcessesByName(processName);
        try
        {
            return processes.Length > 0 ? (uint)processes[0].Id : throw new AppNotRunningException(processName);
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static List<AudioApp> FindAudioApps()
    {
        var ownProcessId = (uint)Environment.ProcessId;
        var apps = new List<AudioApp>();

        using var enumerator = new MMDeviceEnumerator();
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            using (device)
            using (var sessions = device.AudioSessionManager.Sessions)
            {
                for (var index = 0; index < sessions.Count; index++)
                {
                    using var session = sessions[index];
                    var processId = session.GetProcessID;
                    if (session.IsSystemSoundsSession
                        || session.State == AudioSessionState.AudioSessionStateExpired
                        || processId == 0
                        || processId == ownProcessId)
                    {
                        continue;
                    }

                    if (TryGetProcessName(processId) is { } processName)
                    {
                        apps.Add(new AudioApp(processId, processName));
                    }
                }
            }
        }

        return apps;
    }

    /// <returns>Null when the process exited after Windows listed its session.</returns>
    private static string? TryGetProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
