using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ChilePrimeRepair.App;

internal static class WindowFrame
{
    internal static void RemoveLightBorder(Window window)
    {
        // Windows 11's DWM border can remain after replacing the native caption.
        // Older Windows versions return an unsupported-attribute HRESULT; no change is needed there.
        uint color = 0xFFFFFFFE; // DWMWA_COLOR_NONE
        _ = DwmSetWindowAttribute(new WindowInteropHelper(window).Handle, 34, ref color, sizeof(uint));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref uint value, uint size);

}
