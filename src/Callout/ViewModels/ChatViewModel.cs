using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Callout.Core.Settings;

namespace Callout.ViewModels;

internal sealed partial class ChatViewModel : ObservableObject, ITranscriptSurface
{
    /// <summary>Enough for a whole match; older lines are dropped so memory stays flat.</summary>
    private const int MaxHistory = 300;

    private static readonly WindowBounds DefaultBounds = new(double.NaN, double.NaN, 420, 560);

    public ChatViewModel(WindowBounds? bounds, bool alwaysOnTop)
    {
        var initial = bounds ?? DefaultBounds;
        Left = initial.Left;
        Top = initial.Top;
        Width = initial.Width;
        Height = initial.Height;
        AlwaysOnTop = alwaysOnTop;
    }

    public ObservableCollection<TranscriptLine> History { get; } = [];

    /// <summary>The user can close the chat window without leaving chat mode; it reopens from the main window.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; set; } = true;

    [ObservableProperty]
    public partial bool AlwaysOnTop { get; set; }

    [ObservableProperty]
    public partial double Left { get; set; }

    [ObservableProperty]
    public partial double Top { get; set; }

    [ObservableProperty]
    public partial double Width { get; set; }

    [ObservableProperty]
    public partial double Height { get; set; }

    /// <summary>Null until Windows has placed the window, so first launch keeps the default placement.</summary>
    public WindowBounds? Bounds => double.IsNaN(Left) || double.IsNaN(Top) ? null : new WindowBounds(Left, Top, Width, Height);

    public void Show(TranscriptLine line)
    {
        History.Add(line);
        if (History.Count > MaxHistory)
        {
            History.RemoveAt(0);
        }
    }

    [RelayCommand]
    private void Clear() => History.Clear();
}
