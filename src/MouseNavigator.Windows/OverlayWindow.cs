using System.Runtime.InteropServices;
using static MouseNavigator.Windows.NativeMethods;
namespace MouseNavigator.Windows;

public readonly record struct OverlayPlacement(double CenterX, double CenterY, double Scale);

public readonly record struct OverlayPanelPlacement(int Left, int Top, double Width, double Height, double Scale);
public static class OverlayWindow
{
    public static void Configure(nint hwnd)
    {
        var style = GetWindowLongPtrW(hwnd, -20).ToInt64();
        SetWindowLongPtrW(hwnd, -20, (nint)((style | 0x80 | 0x08000000) & ~(0x40000 | 0x100 | 0x200 | 0x20000 | 0x1)));
        // Match the native client area to our physical hit-test and DWM rectangles.
        // The presenter can leave a thin non-client frame even with its title bar hidden.
        var windowStyle=GetWindowLongPtrW(hwnd,-16).ToInt64();
        SetWindowLongPtrW(hwnd,-16,(nint)((windowStyle & ~0x00C40000L) | 0x80000000L));
        SetWindowPos(hwnd,0,0,0,0,0,0x1 | 0x2 | 0x4 | 0x10 | 0x20);
    }
    public static OverlayPlacement Show(nint hwnd, int x, int y, double size)
    {
        var monitor = MonitorFromPoint(new() { X = x, Y = y }, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfoW(monitor, ref info)) throw new InvalidOperationException("无法获取显示器工作区。");
        var pixels = (int)Math.Round(RingDiameterAt(x, y, size) * ScaleAt(x, y));
        var left = Math.Clamp(x - pixels / 2, info.Work.Left, Math.Max(info.Work.Left, info.Work.Right - pixels));
        var top = Math.Clamp(y - pixels / 2, info.Work.Top, Math.Max(info.Work.Top, info.Work.Bottom - pixels));
        PrepareAt(hwnd, left, top, pixels, pixels);
        var region = CreateEllipticRgn(0, 0, pixels + 1, pixels + 1);
        if (region != 0 && SetWindowRgn(hwnd, region, true) == 0) DeleteObject(region);
        if(!SetWindowPos(hwnd, -1, left, top, pixels, pixels, 0x10 | 0x40))
            throw new InvalidOperationException("无法显示悬浮菜单。");
        // The hidden move has already delivered WM_DPICHANGED before the final physical size.
        return new(left + pixels / 2d, top + pixels / 2d, pixels / size);
    }
    // Return the same rounded DIP diameter used by the native overlay, including
    // the monitor work-area limit. The editor scrolls instead of shrinking to fit.
    public static double RingDiameterForWindow(nint hwnd, double size)
    {
        if (hwnd == 0 || !GetWindowRect(hwnd, out var rect)) return size;
        return RingDiameterAt((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2, size);
    }
    public static double RingDiameterAt(int x, int y, double size)
    {
        var monitor = MonitorFromPoint(new() { X = x, Y = y }, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfoW(monitor, ref info)) throw new InvalidOperationException("无法获取显示器工作区。");
        var dpiScale = ScaleAt(x, y);
        return Math.Round(Math.Min(size * dpiScale,
            Math.Max(1, Math.Min(info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top) - 24))) / dpiScale;
    }
    public static OverlayPanelPlacement PanelPlacement(int x, int y)
    {
        var monitor = MonitorFromPoint(new() { X = x, Y = y }, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfoW(monitor, ref info)) throw new InvalidOperationException("无法获取显示器工作区。");
        var scale = ScaleAt(x, y);
        var width = Math.Min(980, (info.Work.Right - info.Work.Left) / scale - 24);
        var height = Math.Min(640, (info.Work.Bottom - info.Work.Top) / scale - 24);
        var pixelsX = (int)Math.Round(width * scale);
        var pixelsY = (int)Math.Round(height * scale);
        var left = Math.Clamp(x - pixelsX / 2, info.Work.Left, info.Work.Right - pixelsX);
        // Open with the pointer over the header where possible, never over an armed target.
        var top = Math.Clamp(y - (int)(32 * scale), info.Work.Top, info.Work.Bottom - pixelsY);
        return new(left, top, width, height, scale);
    }
    private static void PrepareAt(nint hwnd, int left, int top, int width, int height)
    {
        // A cross-monitor SetWindowPos can synchronously trigger WM_DPICHANGED,
        // whose suggested rectangle WinUI applies using the previous monitor DPI.
        // Complete that transition while hidden, then apply our physical rectangle.
        if(!SetWindowPos(hwnd, 0, left, top, width, height, 0x10 | 0x80 | 0x4))
            throw new InvalidOperationException("无法定位悬浮窗口。");
    }
    public static void ShowPanel(nint hwnd, OverlayPanelPlacement placement)
    {
        PrepareAt(hwnd, placement.Left, placement.Top, (int)Math.Round(placement.Width * placement.Scale),
            (int)Math.Round(placement.Height * placement.Scale));
        SetWindowRgn(hwnd, 0, false);
        if (!SetWindowPos(hwnd, -1, placement.Left, placement.Top, (int)Math.Round(placement.Width * placement.Scale),
            (int)Math.Round(placement.Height * placement.Scale), 0x10 | 0x40))
            throw new InvalidOperationException("无法显示窗口预览。");
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
