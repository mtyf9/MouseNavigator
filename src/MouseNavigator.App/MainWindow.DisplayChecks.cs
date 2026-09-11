#if DEBUG
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using MouseNavigator.Core;
using MouseNavigator.Windows;
namespace MouseNavigator.App;
public sealed partial class MainWindow
{
    internal async Task CheckDisplaysAsync(List<string> results,string directory)
    {
        var available=DisplayArea.FindAll();
        var displays=Enumerable.Range(0,available.Count).Select(i=>available[i]).ToArray();
        results.Add($"INFO: {displays.Length} connected displays");
        var ring=new RingWindow();var panel=new WindowPreviewWindow();
        try
        {
            foreach(var display in displays.Concat(displays.Reverse()))
            {
                var work=display.WorkArea;var x=work.X+work.Width/2;var y=work.Y+work.Height/2;
                var dpi=OverlayWindow.ScaleAt(x,y);
                var before=WindowCatalog.Foreground;
                ring.SetMenu(DefaultProfiles.Create()[0],registry);ring.ShowAt(x,y);
                var first=ring.AppWindow.Size;
                await Task.Delay(180);
                var wanted=OverlayWindow.RingDiameterAt(x,y,320)*dpi;
                var actual=ring.AppWindow.Size;
                var root=(FrameworkElement)ring.Content;
                var transform=ring.ViewForSmoke.TransformToVisual(root);
                var a=transform.TransformPoint(new(0,0));var b=transform.TransformPoint(new(320,0));
                var rendered=(b.X-a.X)*(root.XamlRoot?.RasterizationScale??0);
                var hit=ring.HitTest((int)ring.PlacementForSmoke.CenterX,(int)(ring.PlacementForSmoke.CenterY-100*ring.PlacementForSmoke.Scale));
                results.Add((Math.Abs(actual.Width-wanted)<=1&&Math.Abs(rendered-wanted)<=1&&hit=="top"&&WindowCatalog.Foreground==before?"PASS: ":"FAIL: ")+
                    $"Ring display {work.X},{work.Y} dpi={dpi}: first={first.Width}, native={actual.Width}, client={ring.AppWindow.ClientSize.Width}, rendered={rendered:0.##}, wanted={wanted:0.##}, focus unchanged={WindowCatalog.Foreground==before}");
                ring.HideRing();
                var expected=OverlayWindow.PanelPlacement(x,y);
                panel.ShowAt(x,y,[]);var firstPanel=panel.AppWindow.Size;await Task.Delay(180);
                var panelRoot=(FrameworkElement)panel.Content;
                var panelWidth=panelRoot.ActualWidth*(panelRoot.XamlRoot?.RasterizationScale??0);
                results.Add((Math.Abs(panel.AppWindow.Size.Width-expected.Width*dpi)<=1&&Math.Abs(panelWidth-expected.Width*dpi)<=1&&WindowCatalog.Foreground==before?"PASS: ":"FAIL: ")+
                    $"Preview display {work.X},{work.Y} dpi={dpi}: first={firstPanel.Width}, native={panel.AppWindow.Size.Width}, rendered={panelWidth:0.##}, wanted={expected.Width*dpi:0.##}");
                panel.HidePreview();
            }
        }
        finally{ring.Close();panel.Close();}
        var presenter=(OverlappedPresenter)AppWindow.Presenter;
        presenter.Maximize();await Task.Delay(180);
        var pageStart=PageContent.TransformToVisual(PageScroll).TransformPoint(new(0,0)).X;
        var spare=PageScroll.ViewportWidth-PageContent.ActualWidth;
        results.Add((Math.Abs(pageStart-spare/2)<1&&PageContent.ActualWidth<=1360.1?"PASS: ":"FAIL: ")+
            $"Maximized content centered: viewport={PageScroll.ViewportWidth:0.##}, content={PageContent.ActualWidth:0.##}, left={pageStart:0.##}");
        presenter.Restore();await Task.Delay(150);
        results.Add((Math.Abs(PageContent.ActualWidth-Math.Min(1360,PageScroll.ViewportWidth))<1?"PASS: ":"FAIL: ")+
            "Restored window content adapts to its viewport width");
        await SmokeScenario.RenderAsync((FrameworkElement)Content,Path.Combine(directory,"responsive-home.png"));
    }
}
#endif