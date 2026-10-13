using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Callout.Interop;

/// <summary>Makes the native title bar dark and, on Windows 11, the exact color of the window background.</summary>
internal static partial class DarkTitleBar
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaCaptionColor = 35;
    private const int Enabled = 1;

    public static void Apply(Window window) => window.SourceInitialized += (_, _) => ApplyNow(window);

    private static void ApplyNow(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;

        // Purely cosmetic: older Windows builds reject these attributes and simply keep the default title bar.
        SetAttribute(handle, DwmwaUseImmersiveDarkMode, Enabled);
        if (window.Background is SolidColorBrush background)
        {
            SetAttribute(handle, DwmwaCaptionColor, ToColorRef(background.Color));
        }
    }

    private static void SetAttribute(nint handle, int attribute, int value) =>
        DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));

    private static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint windowHandle, int attribute, ref int value, int valueSize);
}
