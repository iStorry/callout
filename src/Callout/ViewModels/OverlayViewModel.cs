using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Callout.ViewModels;

internal sealed partial class OverlayViewModel : ObservableObject, ITranscriptSurface
{
    private const int MaxVisibleLines = 4;
    private static readonly TimeSpan LineLifetime = TimeSpan.FromSeconds(12);

    // Fires once when the oldest line is due to disappear rather than polling.
    private readonly DispatcherTimer _expiryTimer = new();

    public OverlayViewModel(double left, double top)
    {
        Left = left;
        Top = top;
        _expiryTimer.Tick += (_, _) => RemoveExpiredLines();
    }

    public ObservableCollection<TranscriptLine> Lines { get; } = [];

    [ObservableProperty]
    public partial double Left { get; set; }

    [ObservableProperty]
    public partial double Top { get; set; }

    /// <summary>When unlocked the overlay can be dragged and shows a placeholder so it can be found.</summary>
    [ObservableProperty]
    public partial bool IsLocked { get; set; } = true;

    public void Show(TranscriptLine line)
    {
        Lines.Add(line);
        while (Lines.Count > MaxVisibleLines)
        {
            Lines.RemoveAt(0);
        }

        ScheduleNextExpiry();
    }

    private void RemoveExpiredLines()
    {
        var cutoff = DateTimeOffset.Now - LineLifetime;
        while (Lines.Count > 0 && Lines[0].ReceivedAt <= cutoff)
        {
            Lines.RemoveAt(0);
        }

        ScheduleNextExpiry();
    }

    private void ScheduleNextExpiry()
    {
        _expiryTimer.Stop();
        if (Lines.Count == 0)
        {
            return;
        }

        var untilOldestExpires = Lines[0].ReceivedAt + LineLifetime - DateTimeOffset.Now;
        _expiryTimer.Interval = untilOldestExpires > TimeSpan.Zero ? untilOldestExpires : TimeSpan.Zero;
        _expiryTimer.Start();
    }
}
