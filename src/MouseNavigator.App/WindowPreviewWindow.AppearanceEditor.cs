using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using MouseNavigator.Windows;
using Windows.Foundation;
namespace MouseNavigator.App;
internal sealed partial class WindowPreviewWindow
{
    private readonly bool editingAppearance;
    private WindowCandidate? editorSelection;
    private bool editorClosed;
    private bool ShowThumbnail(DwmThumbnail thumbnail,PreviewRect rect)
    {
        if(!editingAppearance)return thumbnail.Show(rect,placement.Scale);
        if(canvas.XamlRoot is null||editorClosed)return false;
        var transform=canvas.TransformToVisual((UIElement)Content);
        var a=transform.TransformPoint(new Point(rect.X,rect.Y));
        var b=transform.TransformPoint(new Point(rect.X+rect.Width,rect.Y+rect.Height));
        return thumbnail.Show(new(a.X,a.Y,b.X-a.X,b.Y-a.Y),canvas.XamlRoot.RasterizationScale);
    }
    public Task<bool> EditAppearanceAsync(FrameworkElement controls,WindowPreviewAppearance initial,int x,int y)
    {
        appearance=initial;
        using var catalog=new WindowCatalog();var windows=catalog.Enumerate();
        placement=OverlayWindow.PanelPlacement(x,y,windows.Count);
        session=new(windows,new WindowPreviewLayout(placement.Width,placement.Height,windows.Count));
        var completion=new TaskCompletionSource<bool>();
        var area=DisplayArea.GetFromPoint(new(x,y),DisplayAreaFallback.Nearest).WorkArea;
        var width=Math.Min(area.Width,(int)((placement.Width+340)*placement.Scale));
        var height=Math.Min(area.Height,(int)(Math.Max(placement.Height,700)*placement.Scale));
        AppWindow.MoveAndResize(new(area.X+(area.Width-width)/2,area.Y+(area.Height-height)/2,width,height));
        var grid=new Grid{ColumnSpacing=12};
        grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        grid.ColumnDefinitions.Add(new(){Width=new GridLength(310)});
        Content=null;
        var box=new Viewbox{Child=canvas,Stretch=Microsoft.UI.Xaml.Media.Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        grid.Children.Add(box);
        var side=new Grid{Padding=new Thickness(10),RowSpacing=10,Background=PreviewPalette.Card(new())};
        side.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        side.RowDefinitions.Add(new(){Height=GridLength.Auto});
        side.Children.Add(new ScrollViewer{Content=controls,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};
        var accept=new Button{Content="确定",Style=(Style)Application.Current.Resources["AccentButtonStyle"]};
        var cancel=new Button{Content="取消"};buttons.Children.Add(accept);buttons.Children.Add(cancel);
        bool accepted=false;
        accept.Click+=(_,_)=>{accepted=true;Close();};cancel.Click+=(_,_)=>Close();
        Grid.SetRow(buttons,1);side.Children.Add(buttons);Grid.SetColumn(side,1);grid.Children.Add(side);
        Content=grid;BuildPage();ApplyEditorAppearance(initial);
        canvas.PointerMoved+=(_,e)=>{var p=e.GetCurrentPoint(canvas).Position;editorSelection=session?.HitTest(p.X,p.Y);UpdateCardState();};
        canvas.PointerExited+=(_,_)=>{editorSelection=null;UpdateCardState();};
        canvas.PointerPressed+=(_,e)=>{
            var p=e.GetCurrentPoint(canvas).Position;
            var direction=session!.Layout.Previous.Contains(p.X,p.Y)?-1:session.Layout.Next.Contains(p.X,p.Y)?1:0;
            if(direction!=0&&session.TurnPage(direction)){BuildPage();UpdateThumbnails();}
        };
        grid.KeyDown+=(_,e)=>{if(e.Key==global::Windows.System.VirtualKey.Escape)Close();};
        grid.LayoutUpdated+=(_,_)=>{if(!editorClosed)UpdateThumbnails();};
        Closed+=(_,_)=>{editorClosed=true;ReleaseThumbnails();completion.TrySetResult(accepted);};
        Activate();
        return completion.Task;
    }
    public void CloseAppearanceEditor(){if(!editorClosed){editorClosed=true;ReleaseThumbnails();Close();}}
    public void ApplyEditorAppearance(WindowPreviewAppearance value)
    {
        appearance=value;canvas.Background=PreviewPalette.Background(value);
        foreach(var card in cards.Values)card.CornerRadius=new(value.CornerRadius);
        foreach(var text in canvas.Children.OfType<TextBlock>())text.Foreground=PreviewPalette.Text(value);
        foreach(var fallback in fallbacks.Values)
            foreach(var child in fallback.Children)
            {
                if(child is TextBlock text)text.Foreground=PreviewPalette.Text(value);
                if(child is FontIcon icon)icon.Foreground=PreviewPalette.Text(value);
            }
        UpdateCardState();
    }
}