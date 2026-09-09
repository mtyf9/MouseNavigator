using System.Runtime.InteropServices;
using MouseNavigator.Core;

namespace MouseNavigator.Windows;

/// <summary>Owns one DWM relationship; coordinates are physical client pixels.</summary>
public sealed class DwmThumbnail : IDisposable
{
    private nint handle;
    private DwmThumbnail(nint handle) => this.handle = handle;
    public static DwmThumbnail? TryCreate(nint destination, WindowIdentity source)
    {
        NativeMethods.GetWindowThreadProcessId(source.Handle, out var pid);
        if (!NativeMethods.IsWindow(source.Handle) || pid != source.ProcessId) return null;
        return DwmRegisterThumbnail(destination, source.Handle, out var thumbnail) >= 0 ? new(thumbnail) : null;
    }
    public bool Show(PreviewRect bounds, double scale)
    {
        if (handle == 0 || DwmQueryThumbnailSourceSize(handle, out var size) < 0 || size.Width <= 0 || size.Height <= 0)
            return false;
        var fit = Math.Min(bounds.Width / size.Width, bounds.Height / size.Height);
        var width = size.Width * fit;
        var height = size.Height * fit;
        var x = bounds.X + (bounds.Width - width) / 2;
        var y = bounds.Y + (bounds.Height - height) / 2;
        var properties = new ThumbnailProperties
        {
            Flags = 0x1 | 0x4 | 0x8 | 0x10,
            Destination = new() { Left = (int)Math.Round(x * scale), Top = (int)Math.Round(y * scale),
                Right = (int)Math.Round((x + width) * scale), Bottom = (int)Math.Round((y + height) * scale) },
            Opacity = 255, Visible = true, ClientAreaOnly = false
        };
        return DwmUpdateThumbnailProperties(handle, ref properties) >= 0;
    }
    public void Dispose()
    {
        if (handle == 0) return;
        DwmUnregisterThumbnail(handle);
        handle = 0;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Size { public int Width, Height; }
    [StructLayout(LayoutKind.Sequential)] private struct ThumbnailProperties
    {
        public uint Flags;
        public NativeMethods.Rect Destination, Source;
        public byte Opacity;
        [MarshalAs(UnmanagedType.Bool)] public bool Visible;
        [MarshalAs(UnmanagedType.Bool)] public bool ClientAreaOnly;
    }
    [DllImport("dwmapi.dll")] private static extern int DwmRegisterThumbnail(nint destination, nint source, out nint thumbnail);
    [DllImport("dwmapi.dll")] private static extern int DwmUnregisterThumbnail(nint thumbnail);
    [DllImport("dwmapi.dll")] private static extern int DwmQueryThumbnailSourceSize(nint thumbnail, out Size size);
    [DllImport("dwmapi.dll")] private static extern int DwmUpdateThumbnailProperties(nint thumbnail, ref ThumbnailProperties properties);
}