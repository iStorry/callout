using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Callout.Interop;

internal static partial class WindowStyles
{
    private const int GwlExStyle = -20;

    [Flags]
    public enum Extended : long
    {
        Transparent = 0x0000_0020,
        ToolWindow = 0x0000_0080,
        NoActivate = 0x0800_0000,
    }

    public static void Set(nint windowHandle, Extended styles, bool enabled)
    {
        var current = GetWindowLongPtr(windowHandle, GwlExStyle);
        var updated = enabled ? current | (nint)styles : current & ~(nint)styles;

        Marshal.SetLastPInvokeError(0);
        if (SetWindowLongPtr(windowHandle, GwlExStyle, updated) == 0 && Marshal.GetLastPInvokeError() != 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial nint GetWindowLongPtr(nint windowHandle, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static partial nint SetWindowLongPtr(nint windowHandle, int index, nint newValue);
}
