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
    private bool previewOnly;
    private Grid? editorGrid;
    private ScrollViewer? editorColors;
    private FrameworkElement? activeColorPanel;
    private global::Windows.Graphics.RectInt32 editorWorkArea;
    private int editorBaseWidth;
    internal void ShowEditorColorPanel(FrameworkElement? content)
    {
        if(editorColors is null||editorGrid is null||editorClosed)return;
        if(ReferenceEquals(activeColorPanel,content))content=null;
        activeColorPanel=content;editorColors.Content=content;
        editorColors.Visibility=content is null?Visibility.Collapsed:Visibility.Visible;
        var width=Math.Min(editorWorkArea.Width,editorBaseWidth+(content is null?0:(int)(320*placement.Scale)));
        AppWindow.MoveAndResize(new(editorWorkArea.X+(editorWorkArea.Width-width)/2,AppWindow.Position.Y,width,AppWindow.Size.Height));
        UpdateEditorColumns();
    }
    private void UpdateEditorColumns()
    {
        if(editorGrid is null||previewOnly)return;
        var available=editorGrid.ActualWidth>0?editorGrid.ActualWidth:AppWindow.Size.Width/placement.Scale;
        var expanded=activeColorPanel is not null;
        editorGrid.ColumnDefinitions[1].Width=new GridLength(Math.Min(320,available*(expanded?.34:.48)));
        editorGrid.ColumnDefinitions[2].Width=new GridLength(expanded?Math.Min(320,available*.34):0);
    }

    private bool ShowThumbnail(DwmThumbnail thumbnail,PreviewRect rect)
    {
        if(!editingAppearance)return thumbnail.Show(rect,placement.Scale,thumbnailOpacity);
        if(canvas.XamlRoot is null||editorClosed)return false;
        var transform=canvas.TransformToVisual((UIElement)Content);
        var a=transform.TransformPoint(new Point(rect.X,rect.Y));
        var b=transform.TransformPoint(new Point(rect.X+rect.Width,rect.Y+rect.Height));
        return thumbnail.Show(new(a.X,a.Y,b.X-a.X,b.Y-a.Y),canvas.XamlRoot.RasterizationScale,thumbnailOpacity);
    }
    public Task<bool> EditAppearanceAsync(FrameworkElement controls,WindowPreviewAppearance initial,int x,int y,bool previewOnly=false)
    {
        this.previewOnly=previewOnly;appearance=initial;
        using var catalog=new WindowCatalog();var windows=catalog.Enumerate();
        placement=OverlayWindow.PanelPlacement(x,y,windows.Count);
        session=new(windows,new WindowPreviewLayout(placement.Width,placement.Height,windows.Count));
        var completion=new TaskCompletionSource<bool>();
        var area=DisplayArea.GetFromPoint(new(x,y),DisplayAreaFallback.Nearest).WorkArea;
        if(previewOnly)
        {
            var presenter=(OverlappedPresenter)AppWindow.Presenter;
            presenter.SetBorderAndTitleBar(false,false);
            presenter.IsResizable=presenter.IsMaximizable=presenter.IsMinimizable=false;
            AppWindow.IsShownInSwitchers=false;
        }
        var width=Math.Min(area.Width,(int)((placement.Width+(previewOnly?0:390))*placement.Scale));
        var height=Math.Min(area.Height,(int)((previewOnly?placement.Height:Math.Max(placement.Height,700))*placement.Scale));
        AppWindow.MoveAndResize(new(area.X+(area.Width-width)/2,area.Y+(area.Height-height)/2,width,height));
        editorWorkArea=area;editorBaseWidth=width;
        var grid=new Grid{Background=previewOnly?null:new Microsoft.UI.Xaml.Media.SolidColorBrush(global::Windows.UI.Color.FromArgb(255,28,28,28))};
        editorGrid=grid;
        grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        grid.ColumnDefinitions.Add(new(){Width=new GridLength(320)});
        grid.ColumnDefinitions.Add(new(){Width=new GridLength(0)});
        editorColors=new ScrollViewer{Visibility=Visibility.Collapsed,Padding=new Thickness(12),HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        Grid.SetColumn(editorColors,2);grid.Children.Add(editorColors);
        grid.SizeChanged+=(_,_)=>UpdateEditorColumns();
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
        if(previewOnly){side.Visibility=Visibility.Collapsed;grid.ColumnDefinitions[1].Width=new GridLength(0);grid.ColumnSpacing=0;}Content=grid;BuildPage();ApplyEditorAppearance(initial);
        canvas.PointerMoved+=(_,e)=>{var p=e.GetCurrentPoint(canvas).Position;editorSelection=session?.HitTest(p.X,p.Y);UpdateCardState();};
        canvas.PointerExited+=(_,_)=>{editorSelection=null;UpdateCardState();};
        canvas.PointerPressed+=(_,e)=>{
            var p=e.GetCurrentPoint(canvas).Position;
            var direction=session!.Layout.Previous.Contains(p.X,p.Y)?-1:session.Layout.Next.Contains(p.X,p.Y)?1:0;
            if(direction!=0&&session.PageCount>1){e.Handled=true;if(session.TurnPage(direction)){BuildPage();PlayTransition();UpdateThumbnails();}}
        };
        grid.KeyDown+=(_,e)=>{if(e.Key==global::Windows.System.VirtualKey.Escape)Close();};
        grid.LayoutUpdated+=(_,_)=>{if(!editorClosed)UpdateThumbnails();};
        Closed+=(_,_)=>{editorClosed=true;transition.Stop();colorMotion.Stop();ReleaseThumbnails();completion.TrySetResult(accepted);};
        if(previewOnly)
        {
            grid.Background=new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            grid.PointerPressed+=(_,e)=>{
                var point=e.GetCurrentPoint(canvas).Position;
                if(session?.HitTest(point.X,point.Y) is null){e.Handled=true;CloseAppearanceEditor();}
            };
            Activated+=(_,e)=>{if(e.WindowActivationState==WindowActivationState.Deactivated)CloseAppearanceEditor();};
            canvas.Loaded+=(_,_)=>{if(!editorClosed)PlayTransition();};
        }
        Activate();
        return completion.Task;
    }
    public void CloseAppearanceEditor(){if(!editorClosed){editorClosed=true;transition.Stop();colorMotion.Stop();ReleaseThumbnails();Close();}}
    public void ApplyEditorAppearance(WindowPreviewAppearance value)
    {
        appearance=value;ApplyBackground();
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