using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    private static Grid SidebarScroll(FrameworkElement content,ScrollViewer scroll)
    {
        scroll.Content=content;scroll.VerticalScrollBarVisibility=ScrollBarVisibility.Hidden;scroll.HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;
        var bar=new ScrollBar{Orientation=Orientation.Vertical,IndicatorMode=ScrollingIndicatorMode.MouseIndicator,Width=10,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,0,-14,0),Minimum=0,SmallChange=24};
        var host=new Grid{Children={scroll,bar}};var updating=false;
        void Refresh()
        {
            updating=true;
            bar.Maximum=scroll.ScrollableHeight;bar.ViewportSize=scroll.ViewportHeight;bar.LargeChange=Math.Max(24,scroll.ViewportHeight*.9);bar.Value=scroll.VerticalOffset;
            bar.Visibility=scroll.ScrollableHeight>0?Visibility.Visible:Visibility.Collapsed;host.Visibility=scroll.Visibility;
            updating=false;
        }
        bar.ValueChanged+=(_,_)=>{if(!updating)scroll.ChangeView(null,bar.Value,null,true);};
        scroll.ViewChanged+=(_,_)=>Refresh();scroll.SizeChanged+=(_,_)=>Refresh();content.SizeChanged+=(_,_)=>Refresh();host.Loaded+=(_,_)=>Refresh();
        scroll.RegisterPropertyChangedCallback(VisibilityProperty,(_,_)=>Refresh());scroll.LayoutUpdated+=(_,_)=>{if(!updating&&(bar.Maximum!=scroll.ScrollableHeight||bar.ViewportSize!=scroll.ViewportHeight))Refresh();};
        return host;
    }
}
