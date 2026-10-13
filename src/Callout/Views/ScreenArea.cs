using System.Windows;

namespace Callout.Views;

internal static class ScreenArea
{
    /// <summary>False when a saved position belongs to a monitor that is no longer connected.</summary>
    public static bool IsVisible(double left, double top) =>
        left >= SystemParameters.VirtualScreenLeft
        && top >= SystemParameters.VirtualScreenTop
        && left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
        && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
}
