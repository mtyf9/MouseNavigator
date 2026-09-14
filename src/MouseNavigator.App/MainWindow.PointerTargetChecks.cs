#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using MouseNavigator.Windows;
using MouseNavigator.Actions.BuiltIn;
namespace MouseNavigator.App;
public sealed partial class MainWindow
{
    internal async Task CheckPointerTargetAsync(List<string> results)
    {
        void Check(bool ok,string text)=>results.Add((ok?"PASS: ":"FAIL: ")+text);
        Activate();await Task.Delay(150);var a=WindowCatalog.Foreground;
        var b=new Window{Title="Pointer target fixture",Content=new Grid{Background=new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DarkGreen)}};
        var bh=WinRT.Interop.WindowNative.GetWindowHandle(b);
        try
        {
            var pos=AppWindow.Position;var size=AppWindow.Size;
            OverlayWindow.Configure(bh);
            b.AppWindow.MoveAndResize(new(pos.X+size.Width/2-250,pos.Y+size.Height/2-200,500,400));
            ((Microsoft.UI.Windowing.OverlappedPresenter)b.AppWindow.Presenter).IsAlwaysOnTop=true;
            a=WindowCatalog.Foreground;b.AppWindow.Show(false);await Task.Delay(150);
            var x=b.AppWindow.Position.X+250;var y=b.AppWindow.Position.Y+200;
            var target=WindowCatalog.WindowAt(x,y);
            Check(target==bh&&a!=bh&&WindowCatalog.Foreground==a,"Inactive B is detected under pointer while A keeps focus");
            var fixture=new PointerCatalog(bh);var platform=new PointerPlatform();var registry=new ActionRegistry();
            registry.Register(new WindowsPlugin(),platform);
            MenuProfile global=new(3,"global","菜单 A",[],[new("one","windows.editing.copy")],IsGlobalDefault:true);
            MenuProfile other=new(3,"b","菜单 B",[new("b.exe")],[new("one","windows.editing.paste")],IsGlobalDefault:false);
            using var navigation=new NavigationController(DispatcherQueue,fixture,registry,new ProfileResolver([global,other]));
            string? matched=null;navigation.ContextChanged+=text=>matched=text;
            var dx=(int)(100*OverlayWindow.ScaleAt(x,y));
            navigation.QueueForSmoke(new(MousePhase.Down,x,y,a,target),new(MousePhase.Move,x+dx,y,0));
            await Task.Delay(120);
            Check(matched?.Contains("菜单 B")==true&&navigation.RingVisibleForSmoke&&WindowCatalog.Foreground==a,"Menu B opens without stealing focus");
            navigation.QueueForSmoke(new MouseSample(MousePhase.Up,x+dx,y,0));await Task.Delay(100);
            Check(platform.Source==bh&&platform.Keys?.SequenceEqual(new ushort[]{17,86})==true&&fixture.Captures==1,"Release dispatches B action to B and keeps the captured target");
        }
        finally{b.Close();}
    }
    private sealed class PointerCatalog(nint b):IWindowCatalog
    {
        public int Captures;
        public IReadOnlyList<WindowCandidate> Enumerate()=>[];
        public MouseNavigator.Contracts.ApplicationContext Capture(nint hwnd){Captures++;return new(hwnd,hwnd==b?"b.exe":"a.exe","fixture");}
    }
    private sealed class PointerPlatform:IPlatformActions
    {
        public nint Source;public ushort[]? Keys;
        public ActionResult SendShortcut(nint source,params ushort[] keys){Source=source;Keys=keys;return ActionResult.Success("recorded");}
    }
}
#endif