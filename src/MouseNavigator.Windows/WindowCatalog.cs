using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using static MouseNavigator.Windows.NativeMethods;
namespace MouseNavigator.Windows;
public sealed class WindowCatalog : IWindowCatalog, IDisposable
{
    private readonly IVirtualDesktopManager desktop = (IVirtualDesktopManager)new VirtualDesktopManager();
    public static nint Foreground => GetForegroundWindow();
    public ApplicationContext Capture(nint hwnd)
    {
        GetWindowThreadProcessId(hwnd, out var pid);
        var name = "";
        try { using var process = Process.GetProcessById((int)pid); name = process.ProcessName; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        return new(hwnd, name, Title(hwnd));
    }
    public IReadOnlyList<WindowCandidate> Enumerate()
    {
        var windows = new List<WindowCandidate>();
        var shell = GetShellWindow();
        EnumWindows((hwnd, _) =>
        {
            if (hwnd == shell || !IsWindowVisible(hwnd)) return true;
            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == Environment.ProcessId) return true;
            var style = GetWindowLongPtrW(hwnd, -20).ToInt64();
            if ((style & (0x80 | 0x08000000)) != 0) return true;
            if (GetWindow(hwnd, 4) != 0 && (style & 0x40000) == 0) return true;
            if (DwmGetWindowAttribute(hwnd, 14, out var cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
            if (desktop.IsWindowOnCurrentVirtualDesktop(hwnd, out var current) < 0 || !current) return true;
            var className = new StringBuilder(256);
            GetClassName(hwnd, className, className.Capacity);
            if (className.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return true;
            var title = Title(hwnd);
            if (!string.IsNullOrWhiteSpace(title)) windows.Add(new(new(hwnd, pid), title));
            return true;
        }, 0);
        return windows;
    }
    internal static string Title(nint hwnd)
    {
        var text = new StringBuilder(1024);
        GetWindowText(hwnd, text, text.Capacity);
        return text.ToString();
    }
    public void Dispose() => Marshal.ReleaseComObject(desktop);
    [ComImport, Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A")] private class VirtualDesktopManager { }
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B")]
    private interface IVirtualDesktopManager
    {
        [PreserveSig] int IsWindowOnCurrentVirtualDesktop(nint hwnd, [MarshalAs(UnmanagedType.Bool)] out bool onCurrentDesktop);
        [PreserveSig] int GetWindowDesktopId(nint hwnd, out Guid desktopId);
        [PreserveSig] int MoveWindowToDesktop(nint hwnd, in Guid desktopId);
    }
}
