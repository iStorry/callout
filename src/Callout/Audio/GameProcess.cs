using System.Diagnostics;

namespace Callout.Audio;

internal sealed class GameNotRunningException() : Exception($"{GameProcess.DisplayName} isn't running yet. Launch the game, then press Start listening.");

internal static class GameProcess
{
    public const string DisplayName = "Valorant";

    /// <summary>The game client itself; VALORANT.exe is only the launcher that starts it.</summary>
    public const string Name = "VALORANT-Win64-Shipping";

    public static uint FindId(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        try
        {
            return processes.Length > 0 ? (uint)processes[0].Id : throw new GameNotRunningException();
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }
}
