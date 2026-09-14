using System.Runtime.InteropServices;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
namespace MouseNavigator.App;
internal sealed class TransparentBackdrop:SystemBackdrop
{
    // One Windows compositor and queue per UI thread, shared by overlay backdrops.
    [ThreadStatic] private static global::Windows.UI.Composition.Compositor? compositor;
    [ThreadStatic] private static nint queueController;
    private global::Windows.UI.Composition.CompositionColorBrush? brush;
    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop target,XamlRoot root)
    {
        if(global::Windows.System.DispatcherQueue.GetForCurrentThread() is null&&queueController==0)
            Marshal.ThrowExceptionForHR(CreateDispatcherQueueController(new(){Size=12,ThreadType=2,Apartment=2},out queueController));
        compositor??=new();brush=compositor.CreateColorBrush(Microsoft.UI.Colors.Transparent);
        target.SystemBackdrop=brush;
        var hwnd=Microsoft.UI.Win32Interop.GetWindowFromWindowId(root.ContentIslandEnvironment.AppWindowId);
        var region=CreateRectRgn(-2,-2,-1,-1);
        try {var blur=new Blur{Flags=3,Enable=1,Region=region};DwmEnableBlurBehindWindow(hwnd,ref blur);}
        finally{DeleteObject(region);}
    }
    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop target)
    {
        target.SystemBackdrop=null;brush?.Dispose();brush=null;
    }
    [StructLayout(LayoutKind.Sequential)] private struct QueueOptions{public uint Size,ThreadType,Apartment;}
    [StructLayout(LayoutKind.Sequential)] private struct Blur{public uint Flags;public int Enable;public nint Region;public int Transition;}
    [DllImport("CoreMessaging.dll")] private static extern int CreateDispatcherQueueController(QueueOptions options,out nint controller);
    [DllImport("dwmapi.dll")] private static extern int DwmEnableBlurBehindWindow(nint hwnd,ref Blur blur);
    [DllImport("gdi32.dll")] private static extern nint CreateRectRgn(int left,int top,int right,int bottom);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint value);
}