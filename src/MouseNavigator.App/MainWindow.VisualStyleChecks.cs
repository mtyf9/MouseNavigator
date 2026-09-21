#if DEBUG
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using MouseNavigator.Windows;
using MouseNavigator.Actions.BuiltIn;
namespace MouseNavigator.App;
public sealed partial class MainWindow
{
    internal async Task CheckVisualStyleAsync(List<string> results)
    {
        var args=Environment.GetCommandLineArgs();var log=Path.Combine(args[Array.IndexOf(args,"--smoke-test")+1],"progress.txt");
        File.WriteAllText(log,"Started visual check"+Environment.NewLine);
        void Check(bool ok,string name){results.Add((ok?"PASS: ":"FAIL: ")+name);File.AppendAllText(log,results[^1]+Environment.NewLine);}
        using var catalog=new WindowCatalog();
        var registry=new ActionRegistry();registry.Register(new WindowsPlugin(),new PlatformActions(catalog));
        var ring=new RingWindow();var panel=new WindowPreviewWindow();
        var menu=DefaultProfiles.Create()[0] with{VisualStyle=new("builtin.glass"),NormalColor="#202020",AccentColor="#FF6020"};
        try
        {
            await CheckBackdropTransparencyAsync(ring,registry,menu,Check);
            foreach(var kind in Enum.GetValues<MenuEntrance>())
            {
                ring.SetMenu(menu with{VisualStyle=menu.VisualStyle! with{Entrance=kind}},registry);
                ring.ShowAt(600,420);
                await Task.Delay(250);
                Check(ring.ViewForSmoke.Opacity==1,"Entrance finishes "+kind);
                Check(ring.ViewForSmoke.HasGlassForSmoke,"Desktop acrylic element "+kind);
                ring.HideRing();
            }
            ring.ShowAt(600,420);
            foreach(var opacity in new[]{0d,.25,1})
            {
                ring.SetMenu(menu with{VisualStyle=new("builtin.glass",GlassOpacity:opacity)},registry);
                ring.ShowAt(600,420);await Task.Delay(100);
                Check(opacity==0?ring.SystemBackdrop is TransparentBackdrop:ring.SystemBackdrop is OverlayAcrylicBackdrop,"Glass opacity endpoint "+opacity);
                ring.HideRing();
            }
            ring.SetMenu(menu,registry);ring.ShowAt(600,420);
            var id=menu.Entries[0].Id;
            ring.Highlight(id);await Task.Delay(180);
            var selected=ring.ViewForSmoke.ButtonColorForSmoke(id);
            for(var i=0;i<10;i++)ring.Highlight(id);
            Check(ring.ViewForSmoke.ButtonColorForSmoke(id)==selected,"Repeated hover preserves color");
            ring.HideRing();ring.ShowAt(600,420);ring.HideRing();
            Check(ring.ViewForSmoke.Opacity==1,"Interrupted entrance resets");
            foreach(var skin in new[]{"builtin.color-ring","example.moon-cat"})
            {
                ring.SetMenu(menu with{VisualStyle=new(skin,GradientStart:"#FF9040",GradientEnd:"#6040FF",GradientAngle:135)},registry);
                ring.ShowAt(600,420);await Task.Delay(250);Check(ring.ViewForSmoke.Opacity==1,"Render skin "+skin);ring.HideRing();
            }
            ring.SetMenu(menu with{VisualStyle=new("community.pet")},registry);
            Check(!ring.ViewForSmoke.HasGlassForSmoke,"Unknown skin falls back safely");
            var gif=Convert.ToBase64String(Convert.FromHexString("47494638396101000100800000FF00000000FF21FF0B4E45545343415045322E30030100000021F904000A0000002C000000000100010000020244010021F904000A0000002C00000000010001000002024C01003B"));
            ring.SetMenu(menu with{VisualStyle=new("builtin.image",BackgroundImage:gif)},registry);
            ring.ShowAt(600,420);await Task.Delay(800);
            Check(ring.ViewForSmoke.BackgroundForSmoke?.AnimatedForSmoke==true,"GIF retains multiple frames");
            Check(ring.ViewForSmoke.BackgroundForSmoke?.PlayingForSmoke==true,"GIF background plays");
            ring.HideRing();Check(ring.ViewForSmoke.BackgroundForSmoke?.PlayingForSmoke==false,"Hidden GIF stops");
            ring.ShowAt(600,420);await Task.Delay(200);
            Check(ring.ViewForSmoke.BackgroundForSmoke?.PlayingForSmoke==true,"GIF resumes when shown");
            ring.HideRing();
            for(var pass=0;pass<3;pass++)
            foreach(var background in new[]{"builtin.glass","builtin.color-ring","example.moon-cat","builtin.image","builtin.solid"})
            {
                ring.SetMenu(menu with{VisualStyle=new(background,BackgroundImage:gif)},registry);
                ring.ShowAt(600,420);await Task.Delay(80);ring.HideRing();
            }
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();await Task.Delay(10000);
            Check(true,"Background switching survives collection and delayed cleanup");
            panel.ShowAt(650,450,catalog.Enumerate(),new(TransitionMilliseconds:300));
            Check(panel.ThumbnailOpacityForSmoke==(SurfaceMotion.Enabled?0:255),"Native thumbnail starts with panel");
            await Task.Delay(420);
            Check(panel.ThumbnailOpacityForSmoke==255&&panel.SurfaceOpacityForSmoke==1,"Preview transition completes");
            Check(panel.ThumbnailCountForSmoke>0,"Live native thumbnails render");
            foreach(var background in new[]{"builtin.solid","builtin.glass","builtin.color-ring","builtin.image"})
            {
                var followMenu=menu with{VisualStyle=new(background,BackgroundImage:gif,SolidColor:"#804020",SolidOpacity:.7),PreviewAppearance=new(FollowMenuBackground:true)};
                panel.ShowAt(650,450,catalog.Enumerate(),PreviewStyle.Resolve(followMenu));await Task.Delay(200);
                Check(panel.IsOpen&&panel.ThumbnailCountForSmoke>0&&(background!="builtin.glass"||panel.SystemBackdrop is OverlayAcrylicBackdrop),"Window preview follows "+background);
                panel.HidePreview();
            }
            panel.HidePreview();panel.ShowAt(650,450,catalog.Enumerate());panel.HidePreview();
            Check(panel.ThumbnailCountForSmoke==0&&panel.ThumbnailOpacityForSmoke==255,"Preview interruption releases thumbnails");
        }
        finally{ring.HideRing();ring.Close();panel.HidePreview();panel.Close();}
    }
}
#endif
