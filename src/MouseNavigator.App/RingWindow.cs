using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Path = Microsoft.UI.Xaml.Shapes.Path;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using MouseNavigator.Windows;
using Windows.Foundation;
using Windows.UI;

namespace MouseNavigator.App;

internal sealed class RingWindow : Window
{
    private readonly Grid surface = new();
    private readonly Dictionary<RingSlot, Path> sectors = [];
    private readonly Dictionary<RingSlot, TextBlock> labels = [];
    private readonly Dictionary<RingSlot, FontIcon> icons = [];
    private readonly TextBlock center = new() { Text = "取消", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly nint hwnd;
    private OverlayPlacement placement;

    public RingWindow()
    {
        Title = "MouseNavigator Ring";
        surface.Background = new SolidColorBrush(Color.FromArgb(255, 30, 34, 44));
        surface.RequestedTheme = ElementTheme.Dark;
        Content = surface;
        foreach (var (slot, angle) in new[] { (RingSlot.Right, 0d), (RingSlot.Bottom, 90d), (RingSlot.Left, 180d), (RingSlot.Top, 270d) })
        {
            var sector = new Path { Data = Wedge(angle), Fill = IdleBrush(), IsHitTestVisible = false };
            surface.Children.Add(sector);
            sectors[slot] = sector;
            var label = new TextBlock { FontSize = 12, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 92 };
            var icon = new FontIcon { FontSize = 23 };
            var panel = new StackPanel { Spacing = 7, Width = 98, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            panel.Children.Add(icon); panel.Children.Add(label);
            var radians = angle * Math.PI / 180;
            panel.Margin = new Thickness(160 + Math.Cos(radians) * 99 - 49, 160 + Math.Sin(radians) * 99 - 26, 0, 0);
            surface.Children.Add(panel);
            labels[slot] = label; icons[slot] = icon;
        }
        surface.Children.Add(new Ellipse { Width = 78, Height = 78, Fill = new SolidColorBrush(Color.FromArgb(255, 42, 48, 61)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        surface.Children.Add(center);
        hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var presenter = (OverlappedPresenter)AppWindow.Presenter;
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        AppWindow.IsShownInSwitchers = false;
        OverlayWindow.Configure(hwnd);
    }

    public void SetMenu(MenuProfile menu, ActionRegistry registry)
    {
        foreach (var slot in Enum.GetValues<RingSlot>())
        {
            var entry = menu.Entries.FirstOrDefault(e => e.Slot == slot);
            var action = entry is null ? null : registry.Find(entry.ActionId);
            labels[slot].Text = action?.Descriptor.Name ?? (entry is null ? "未配置" : "动作不可用");
            icons[slot].Glyph = action?.Descriptor.Glyph ?? "\uE711";
        }
    }

    public void ShowAt(int x, int y)
    {
        Highlight(null);
        placement = OverlayWindow.Show(hwnd, x, y, RingGeometry.Size);
    }
    public RingSlot? HitTest(int x, int y) => RingGeometry.HitTest((x - placement.CenterX) / placement.Scale, (y - placement.CenterY) / placement.Scale);
    public void HideRing() => OverlayWindow.Hide(hwnd);
    public void Highlight(RingSlot? selected)
    {
        foreach (var (slot, sector) in sectors)
            sector.Fill = slot == selected ? new SolidColorBrush(Color.FromArgb(255, 57, 104, 183)) : IdleBrush();
        center.Text = selected is null ? "取消" : "松开执行";
    }
    private static SolidColorBrush IdleBrush() => new(Color.FromArgb(255, 39, 45, 58));
    private static PathGeometry Wedge(double angle)
    {
        Point P(double radius, double degrees) => new(160 + radius * Math.Cos(degrees * Math.PI / 180), 160 + radius * Math.Sin(degrees * Math.PI / 180));
        var a = angle - 43; var b = angle + 43;
        var figure = new PathFigure { StartPoint = P(151, a), IsClosed = true };
        figure.Segments.Add(new ArcSegment { Point = P(151, b), Size = new(151, 151), SweepDirection = SweepDirection.Clockwise });
        figure.Segments.Add(new LineSegment { Point = P(46, b) });
        figure.Segments.Add(new ArcSegment { Point = P(46, a), Size = new(46, 46), SweepDirection = SweepDirection.Counterclockwise });
        return new PathGeometry { Figures = { figure } };
    }
}
