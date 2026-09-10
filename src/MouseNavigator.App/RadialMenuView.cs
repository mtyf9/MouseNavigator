using System.Diagnostics;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace MouseNavigator.App;

internal sealed record MenuButtonVisual(string Id, string Label, string Glyph, ActionInteraction Interaction, int Ring, string? Image);

/// <summary>The editor and floating window share geometry, appearance and center content.</summary>
internal sealed class RadialMenuView : UserControl
{
    internal const string CenterButtonId = "$center";
    private readonly Canvas canvas = new() { Background = new SolidColorBrush(Colors.Transparent) };
    private readonly Canvas backdrop = new() { IsHitTestVisible = false };
    private readonly Canvas buttonLayer = new() { IsHitTestVisible = false };
    private readonly Ellipse centerDisk = new() { Fill = Brush(42, 48, 61) };
    private readonly Dictionary<string, Tile> tiles = [];
    private readonly DispatcherTimer animation = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly TextBlock center = new() { FontSize = 12, TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 70 };
    private readonly StackPanel centerContent = new() { Spacing = 3, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Grid centerHub = new() { Width = 78, Height = 78, IsHitTestVisible = false };
    private MenuProfile menu = new(3, "empty", "Empty", [], []);
    private string? highlighted, draggedId;
    private long animationStart;
    private const double AnimationMilliseconds = 210;
    private readonly record struct Pose(double Angle, double Inner, double Outer, double Half, double Width);
    private sealed class Tile
    {
        public required MenuButtonVisual Visual;
        public double IconSize;
        public readonly Path Sector = new();
        public readonly StackPanel Panel = new() { Spacing = 6, IsHitTestVisible = false };
        public readonly TextBlock Label = new() { TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
            MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = HorizontalAlignment.Center };
        public Pose Current, From, Target;
    }
    public IReadOnlyList<MenuButtonVisual> Buttons { get; private set; } = [];
    public MultiRingLayout Layout => new(menu);
    public double Diameter => Layout.Diameter;
    public bool IsEditor { get; set; }
    public event Action<string>? ButtonSelected;
    public event Action? CenterSelected;
    public event Action<string, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs>? ButtonPressed;
    public RadialMenuView()
    {
        Content = canvas; RequestedTheme = ElementTheme.Dark;
        centerHub.Children.Add(centerDisk); centerHub.Children.Add(centerContent);
        canvas.Children.Add(backdrop); canvas.Children.Add(buttonLayer); canvas.Children.Add(centerHub);
        animation.Tick += (_, _) => AdvanceAnimation();
        Unloaded += (_, _) => FinishAnimation();
        canvas.PointerPressed += (_, e) =>
        {
            var point = e.GetCurrentPoint(canvas);
            if (!point.Properties.IsLeftButtonPressed) return;
            var id = HitTest(point.Position.X - Diameter / 2, point.Position.Y - Diameter / 2);
            if (IsEditor && id is not null) { ButtonPressed?.Invoke(id, e); e.Handled = true; }
            else e.Handled = SelectAt(point.Position.X - Diameter / 2, point.Position.Y - Diameter / 2);
        };
    }
    public void SetMenu(MenuProfile menu, Func<string, ActionDescriptor?> resolve)
    {
        if (animation.IsEnabled) AdvanceAnimation();
        var animate = IsEditor && IsLoaded && this.menu.Id == menu.Id && this.menu.RingCount == menu.RingCount
            && new global::Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
        var nextButtons = menu.Entries.Select(e =>
        {
            var action = resolve(e.ActionId);
            return new MenuButtonVisual(e.Id, e.Label ?? action?.Name ?? (e.ActionId.Length == 0 ? "未配置" : "动作不可用"),
                e.Glyph ?? action?.Glyph ?? "\uE711", action?.Interaction ?? ActionInteraction.Invoke, e.Ring, e.Image);
        }).ToArray();
        // Pointer updates and property refreshes with the same destination must not
        // recreate visuals or restart a movement already in progress.
        if (this.menu.Id == menu.Id && this.menu.RingCount == menu.RingCount && Buttons.SequenceEqual(nextButtons)
            && Enumerable.Range(0,menu.RingCount).All(r=>Layout.Rotation(r)==new MultiRingLayout(menu).Rotation(r))
            && this.menu.CenterText == menu.CenterText && this.menu.CenterImage == menu.CenterImage && this.menu.CenterGlyph == menu.CenterGlyph) { this.menu = menu; return; }
        this.menu = menu; Buttons = nextButtons;
        foreach (var id in tiles.Keys.Except(Buttons.Select(b => b.Id)).ToArray())
        {
            buttonLayer.Children.Remove(tiles[id].Sector); buttonLayer.Children.Remove(tiles[id].Panel); tiles.Remove(id);
        }
        backdrop.Children.Clear();
        Width = Height = canvas.Width = canvas.Height = Diameter;
        var middle = Diameter / 2;
        backdrop.Width = backdrop.Height = buttonLayer.Width = buttonLayer.Height = Diameter;
        backdrop.Children.Add(new Ellipse { Width = Diameter, Height = Diameter, Fill = Brush(30, 34, 44) });
        for (var r = 0; r < menu.RingCount; r++)
        {
            var outline = new Ellipse { Width = Layout.Outer(r) * 2, Height = Layout.Outer(r) * 2,
                Stroke = Brush(67, 77, 94), StrokeThickness = 1, IsHitTestVisible = false };
            Place(outline, middle - Layout.Outer(r), middle - Layout.Outer(r)); backdrop.Children.Add(outline);
            if (!Buttons.Any(b => b.Ring == r) && IsEditor)
            {
                var empty = new TextBlock { Text = $"拖入第 {r + 1} 圈", FontSize = 12, Opacity = 0.65, IsHitTestVisible = false };
                Place(empty, middle - 45, middle - (Layout.Inner(r) + Layout.Outer(r)) / 2 - 8); backdrop.Children.Add(empty);
            }
        }
        var changed = false;
        foreach (var button in Buttons)
        {
            var group = Buttons.Where(b => b.Ring == button.Ring).ToList();
            var count = group.Count;
            var angle = RingGeometry.Angle(group.IndexOf(button), count) + Layout.Rotation(button.Ring);
            var radius = (Layout.Inner(button.Ring) + Layout.Outer(button.Ring)) / 2;
            var width = Math.Min(100, 2 * radius * Math.Sin(Math.PI / Math.Max(3, count)) - 8);
            var target = new Pose(angle, Layout.Inner(button.Ring), Layout.Outer(button.Ring), 180d / count - RingGeometry.GapDegrees, width);
            var exists = tiles.TryGetValue(button.Id, out var tile);
            if (!exists) { tile = new Tile { Visual = button, Current = target }; tiles.Add(button.Id, tile); }
            var t = tile!;
            var iconSize = count > 8 ? 21 : 23;
            if (!exists || t.Visual != button || t.IconSize != iconSize)
            {
                t.Visual = button; t.IconSize = iconSize; t.Panel.Children.Clear();
                t.Panel.Children.Add(ButtonIcons.Create(button.Glyph, button.Image, iconSize));
                t.Panel.Children.Add(t.Label); t.Label.Text = button.Label;
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(t.Sector, button.Label);
                ToolTipService.SetToolTip(t.Sector, button.Label);
            }
            t.Label.FontSize = count > 8 ? 10 : 12;
            // Take the shortest arc, retaining the current frame when a drag changes direction.
            target = target with { Angle = t.Current.Angle + ((angle - t.Current.Angle + 540) % 360 + 360) % 360 - 180 };
            t.From = t.Current; t.Target = target;
            if (!animate || !exists) t.Current = target;
            changed |= t.Current != t.Target;
            t.Sector.Width = t.Sector.Height = Diameter;
            if (!exists) { buttonLayer.Children.Add(t.Sector); buttonLayer.Children.Add(t.Panel); }
            ApplyPose(t);
        }
        centerContent.Children.Clear();
        if (menu.CenterImage is not null || menu.CenterGlyph is not null) centerContent.Children.Add(ButtonIcons.Create(menu.CenterGlyph, menu.CenterImage, 29));
        centerContent.Children.Add(center);
        Place(centerHub, middle - 39, middle - 39);
        Highlight(highlighted); SetDraggedButton(draggedId);
        animation.Stop();
        if (changed) { animationStart = Stopwatch.GetTimestamp(); animation.Start(); }
    }
    public void SetDraggedButton(string? id)
    {
        draggedId = id;
        foreach (var (key, tile) in tiles)
        {
            tile.Panel.Opacity = key == id ? 0 : 1;
            tile.Sector.Opacity = key == id ? 0.4 : 1;
        }
    }
    private void AdvanceAnimation()
    {
        var progress = Math.Clamp(Stopwatch.GetElapsedTime(animationStart).TotalMilliseconds / AnimationMilliseconds, 0, 1);
        var eased = 1 - Math.Pow(1 - progress, 3);
        foreach (var tile in tiles.Values)
        {
            var a = tile.From; var b = tile.Target;
            double Mix(double x, double y) => x + (y - x) * eased;
            tile.Current = new(Mix(a.Angle,b.Angle), Mix(a.Inner,b.Inner), Mix(a.Outer,b.Outer), Mix(a.Half,b.Half), Mix(a.Width,b.Width));
            ApplyPose(tile);
        }
        if (progress >= 1) animation.Stop();
    }
    private void FinishAnimation()
    {
        animation.Stop();
        foreach (var tile in tiles.Values) { tile.Current = tile.Target; ApplyPose(tile); }
    }
    private void ApplyPose(Tile tile)
    {
        var p = tile.Current; tile.Sector.Data = Wedge(p);
        tile.Panel.Width = tile.Label.MaxWidth = p.Width;
        var radius = (p.Inner + p.Outer) / 2;
        Place(tile.Panel, Diameter / 2 + Math.Cos(p.Angle * Math.PI / 180) * radius - p.Width / 2,
            Diameter / 2 + Math.Sin(p.Angle * Math.PI / 180) * radius - 26);
    }
    public string? HitTest(double dx, double dy) => Layout.HitTest(dx,dy);
    internal bool SelectAt(double dx, double dy)
    {
        if (IsEditor && dx * dx + dy * dy <= 39 * 39) { CenterSelected?.Invoke(); return true; }
        var id = HitTest(dx, dy); if (id is null) return false;
        ButtonSelected?.Invoke(id); return true;
    }
    public void Highlight(string? id)
    {
        highlighted = id;
        centerDisk.Fill = IsEditor && id == CenterButtonId ? Brush(57,104,183) : Brush(42,48,61);
        foreach (var (key, tile) in tiles) tile.Sector.Fill = key == id ? Brush(57,104,183) : Brush(39,45,58);
        var selected = Buttons.FirstOrDefault(b => b.Id == id);
        center.Text = menu.CenterText ?? (IsEditor && id == CenterButtonId ? "松开执行" : selected is null ? "取消" : selected.Interaction == ActionInteraction.WindowPreview ? "进入预览" : "松开执行");
        center.Visibility = center.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }
    private PathGeometry Wedge(Pose p)
    {
        Point P(double radius, double degrees) => new(Diameter / 2 + radius * Math.Cos(degrees * Math.PI / 180),
            Diameter / 2 + radius * Math.Sin(degrees * Math.PI / 180));
        var a = p.Angle - p.Half; var b = p.Angle + p.Half;
        var figure = new PathFigure { StartPoint = P(p.Outer, a), IsClosed = true };
        figure.Segments.Add(new ArcSegment { Point = P(p.Outer, b), Size = new(p.Outer,p.Outer), IsLargeArc = p.Half > 90, SweepDirection = SweepDirection.Clockwise });
        figure.Segments.Add(new LineSegment { Point = P(p.Inner,b) });
        figure.Segments.Add(new ArcSegment { Point = P(p.Inner,a), Size = new(p.Inner,p.Inner), IsLargeArc = p.Half > 90, SweepDirection = SweepDirection.Counterclockwise });
        return new PathGeometry { Figures = { figure } };
    }
    private static void Place(DependencyObject element, double x, double y) { Canvas.SetLeft((UIElement)element,x); Canvas.SetTop((UIElement)element,y); }
    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(ColorHelper.FromArgb(255,r,g,b));
#if DEBUG
    internal bool IsAnimatingForSmoke => animation.IsEnabled;
    internal string CenterTextForSmoke => center.Text;
    internal bool HasCenterGlyphForSmoke => menu.CenterGlyph is not null && centerContent.Children.FirstOrDefault() is FontIcon icon && icon.Glyph == menu.CenterGlyph;
    internal bool HasCenterImageForSmoke => menu.CenterImage is not null && centerContent.Children.Count == 2;
    internal double AngleForSmoke(string id) => tiles[id].Current.Angle;
    internal double OpacityForSmoke(string id) => tiles[id].Panel.Opacity;
#endif
}