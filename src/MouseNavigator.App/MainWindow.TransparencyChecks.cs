#if DEBUG
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
namespace MouseNavigator.App;
public sealed partial class MainWindow
{
    private static async Task CheckBackdropTransparencyAsync(RingWindow ring,ActionRegistry registry,MenuProfile menu,Action<bool,string> check)
    {
        var fill=new Border{Background=new SolidColorBrush(Microsoft.UI.Colors.White)};
        var behind=new Window{Content=fill,Title="MouseNavigator transparency check"};
        behind.AppWindow.MoveAndResize(new(360,180,600,550));behind.Activate();
        var image=Convert.ToBase64String(Convert.FromHexString("47494638396101000100800000FF00000000FF21F90400000000002C00000000010001000002024401003B"));
        var transparent=menu with{Entries=[],CenterText="",CenterGlyph=null,CenterImage=null,NormalOpacity=1,VisualStyle=new("builtin.image",Entrance:MenuEntrance.None,BackgroundImage:image,BackgroundOpacity:0,OutlineEnabled:false)};
        try
        {
            ring.SetMenu(transparent,registry);ring.ShowAt(600,420);await Task.Delay(350);
            uint Sample()
            {
                var dc=GetDC(0);try{return GetPixel(dc,600,420);}finally{ReleaseDC(0,dc);}
            }
            var white=Sample();
            ring.HideRing();fill.Background=new SolidColorBrush(Microsoft.UI.Colors.Lime);await Task.Delay(350);
            check(((Sample()>>8)&255)>220&&(Sample()&255)<30,"Underlying test window rendered green");
            ring.ShowAt(600,420);await Task.Delay(250);
            var green=Sample();
            check((white&255)>220&&(green&255)<30&&((green>>8)&255)>220,$"Zero image opacity reveals underlying window ({white:X6}/{green:X6})");
            ring.SetMenu(transparent with{VisualStyle=transparent.VisualStyle! with{BackgroundOpacity=.5}},registry);await Task.Delay(250);
            var blend=Sample();
            check((blend&255)>70&&((blend>>8)&255)>70,$"Half image opacity blends with underlying content ({blend:X6})");
            ring.SetMenu(transparent with{VisualStyle=new("builtin.solid",Entrance:MenuEntrance.None,SolidColor:"#FF0000",SolidOpacity:0,OutlineEnabled:false)},registry);await Task.Delay(180);
            var clearSolid=Sample();
            check((clearSolid&255)<30&&((clearSolid>>8)&255)>220,"Transparent solid background has no blue residual plate");
            ring.SetMenu(transparent with{VisualStyle=new("builtin.solid",Entrance:MenuEntrance.None,SolidColor:"#FF0000",SolidOpacity:1,OutlineEnabled:false)},registry);await Task.Delay(180);
            var opaqueSolid=Sample();
            check((opaqueSolid&255)>200&&((opaqueSolid>>8)&255)<40,"Opaque solid background keeps chosen color");
        }
        finally{ring.HideRing();behind.Close();}
    }
    [DllImport("user32.dll")] private static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hwnd,nint dc);
    [DllImport("gdi32.dll")] private static extern uint GetPixel(nint dc,int x,int y);
}
#endif
