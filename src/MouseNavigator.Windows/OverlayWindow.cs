using System.Runtime.InteropServices;
using static MouseNavigator.Windows.NativeMethods;
namespace MouseNavigator.Windows;

public readonly record struct OverlayPlacement(double CenterX, double CenterY, double Scale);

public static class OverlayWindow
{
    public static void Configure(nint hwnd)
    {
        var style = GetWindowLongPtrW(hwnd, -20).ToInt64();
        SetWindowLongPtrW(hwnd, -20, (nint)((style | 0x80 | 0x08000000) & ~0x40000));
    }
    public static OverlayPlacement Show(nint hwnd, int x, int y, double size)
    {
        var monitor = MonitorFromPoint(new() { X = x, Y = y }, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfoW(monitor, ref info)) throw new InvalidOperationException("无法获取显示器工作区。");
        GetDpiForMonitor(monitor, 0, out var dpi, out _);
        var scale = (dpi == 0 ? 96 : dpi) / 96d;
        var pixels = (int)Math.Round(size * scale);
        var left = Math.Clamp(x - pixels / 2, info.Work.Left, Math.Max(info.Work.Left, info.Work.Right - pixels));
        var top = Math.Clamp(y - pixels / 2, info.Work.Top, Math.Max(info.Work.Top, info.Work.Bottom - pixels));
        var region = CreateEllipticRgn(0, 0, pixels + 1, pixels + 1);
        if (region != 0 && SetWindowRgn(hwnd, region, true) == 0) DeleteObject(region);
        SetWindowPos(hwnd, -1, left, top, pixels, pixels, 0x10 | 0x40);
        // WinUI updates its rasterization scale after the native move to the target monitor.
        return new(left + pixels / 2d, top + pixels / 2d, scale);
    }
    public static double WindowScale(nint hwnd) => Math.Max(96u, GetDpiForWindow(hwnd)) / 96d;
    public static double ScaleAt(int x, int y)
    {
        var monitor = MonitorFromPoint(new() { X = x, Y = y }, 2);
        GetDpiForMonitor(monitor, 0, out var dpi, out _);
        return (dpi == 0 ? 96 : dpi) / 96d;
    }
    public static void Hide(nint hwnd) => ShowWindow(hwnd, 0);
}
