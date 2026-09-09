using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MouseNavigator.Core;
using MouseNavigator.Windows;

namespace MouseNavigator.App;

/// <summary>Non-activating selection surface. DWM owns the live image; WinUI owns the cards.</summary>
internal sealed class WindowPreviewWindow : Window
{
    private readonly Canvas canvas = new();
    private readonly nint hwnd;
    private readonly Dictionary<int, Border> cards = [];
    private readonly Dictionary<int, TextBlock> statuses = [];
    private readonly Dictionary<int, StackPanel> fallbacks = [];
    private readonly Dictionary<int, DwmThumbnail> thumbnails = [];
    private WindowPreviewSession? session;
    private OverlayPanelPlacement placement;
    private TextBlock? previous, next, hint;
    private bool armed;
    private int openingX, openingY, lastX, lastY, pageDirection;
    private long pageAfter;
    public bool IsOpen { get; private set; }

    public WindowPreviewWindow()
    {
        Title = "MouseNavigator 窗口预览";
        Content = canvas;
        canvas.Background = Brush(25, 29, 38);
        canvas.RequestedTheme = ElementTheme.Dark;
        hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var presenter = (OverlappedPresenter)AppWindow.Presenter;
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = presenter.IsMaximizable = presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        AppWindow.IsShownInSwitchers = false;
        OverlayWindow.Configure(hwnd);
    }
    public void ShowAt(int x, int y, IReadOnlyList<WindowCandidate> windows)
    {
        HidePreview();
        placement = OverlayWindow.PanelPlacement(x, y);
        session = new(windows, new WindowPreviewLayout(placement.Width, placement.Height));
        openingX = lastX = x; openingY = lastY = y;
        armed = false; pageDirection = 0;
        BuildPage();
        OverlayWindow.ShowPanel(hwnd, placement);
        IsOpen = true;
        // Native thumbnail rectangles are physical pixels, independent of XAML layout timing.
        UpdateThumbnails();
    }
    private void BuildPage()
    {
        ReleaseThumbnails();
        cards.Clear(); statuses.Clear(); fallbacks.Clear(); canvas.Children.Clear();
        if (session is null) return;
        var layout = session.Layout;
        canvas.Width = layout.Width; canvas.Height = layout.Height;
        Place(new TextBlock { Text = "窗口预览", FontSize = 23, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold }, new(20, 14, 180, 32));
        hint = new TextBlock { Text = "保持按住中键 · 移到窗口后松开切换 · Esc 取消", FontSize = 12, Opacity = 0.7 };
        Place(hint, new(20, 46, layout.Width - 40, 22));
        foreach (var (index, window) in session.Visible)
        {
            var rect = layout.Card(index % layout.Capacity);
            var image = layout.Image(index % layout.Capacity);
            var card = new Border { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(2),
                Background = Brush(36, 42, 54), BorderBrush = Brush(57, 67, 85) };
            cards[index] = card;
            Place(card, rect);
            var fallback = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center };
            fallback.Children.Add(new FontIcon { Glyph = "\uE737", FontSize = 30, Opacity = 0.55 });
            var status = new TextBlock { Text = "预览不可用", FontSize = 12, Opacity = 0.65, HorizontalAlignment = HorizontalAlignment.Center };
            fallback.Children.Add(status);
            statuses[index] = status;
            fallbacks[index] = fallback;
            Place(new Grid { Children = { fallback } }, image);
            Place(new TextBlock { Text = window.Title, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center }, new(rect.X + 12, rect.Y + rect.Height - 36, rect.Width - 24, 28));
            if (session.IsAvailable(window.Identity))
            {
                var thumbnail = DwmThumbnail.TryCreate(hwnd, window.Identity);
                if (thumbnail is not null) thumbnails[index] = thumbnail;
            }
        }
        if (session.Windows.Count == 0)
            Place(new TextBlock { Text = "当前桌面没有可预览的窗口\n在空白处松开中键即可返回", TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap, FontSize = 18, VerticalAlignment = VerticalAlignment.Center },
                new(24, 110, layout.Width - 48, layout.Height - 210));
        previous = new TextBlock { Text = "‹  停留上一页", FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
        next = new TextBlock { Text = "停留下一页  ›", FontSize = 13, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        Place(previous, layout.Previous); Place(next, layout.Next);
        Place(new TextBlock { Text = $"{session.Page + 1} / {session.PageCount}   ·   {session.Windows.Count} 个窗口",
            TextAlignment = TextAlignment.Center, FontSize = 12, Opacity = 0.65, VerticalAlignment = VerticalAlignment.Center },
            new(132, layout.Height - 48, layout.Width - 264, 32));
        UpdateCardState();
    }
    private void Place(FrameworkElement element, PreviewRect rect)
    {
        element.Width = Math.Max(0, rect.Width); element.Height = Math.Max(0, rect.Height);
        Canvas.SetLeft(element, rect.X); Canvas.SetTop(element, rect.Y);
        canvas.Children.Add(element);
    }
    public void MovePointer(int x, int y)
    {
        if (!IsOpen || session is null) return;
        lastX = x; lastY = y;
        var dx = (x - (double)openingX) / placement.Scale;
        var dy = (y - (double)openingY) / placement.Scale;
        if (dx * dx + dy * dy >= 144) armed = true;
        UpdateCardState();
        var localX = (x - placement.Left) / placement.Scale;
        var localY = (y - placement.Top) / placement.Scale;
        var direction = !armed ? 0 : session.Layout.Previous.Contains(localX, localY) && session.Page > 0 ? -1
            : session.Layout.Next.Contains(localX, localY) && session.Page + 1 < session.PageCount ? 1 : 0;
        if (direction != pageDirection) { pageDirection = direction; pageAfter = Environment.TickCount64 + 650; }
    }
    public WindowCandidate? SelectionAt(int x, int y) => !armed || session is null ? null
        : session.HitTest((x - placement.Left) / placement.Scale, (y - placement.Top) / placement.Scale);
    public void RefreshAvailable(IReadOnlyList<WindowCandidate> current)
    {
        session?.RefreshAvailable(current);
        UpdateCardState();
    }
    public void Tick()
    {
        if (!IsOpen || session is null) return;
        if (pageDirection != 0 && Environment.TickCount64 >= pageAfter)
        {
            if (session.TurnPage(pageDirection)) { BuildPage(); UpdateThumbnails(); }
            pageAfter = Environment.TickCount64 + 650;
        }
    }
    private void UpdateCardState()
    {
        if (session is null) return;
        var selected = SelectionAt(lastX, lastY);
        foreach (var (index, window) in session.Visible)
        {
            var alive = session.IsAvailable(window.Identity);
            cards[index].BorderBrush = alive && selected?.Identity == window.Identity ? Brush(115, 175, 255) : Brush(57, 67, 85);
            cards[index].Opacity = alive ? 1 : 0.4;
            if (!alive)
            {
                fallbacks[index].Visibility = Visibility.Visible;
                statuses[index].Text = "窗口已关闭或已离开当前桌面";
                if (thumbnails.Remove(index, out var thumbnail)) thumbnail.Dispose();
            }
        }
        if (previous is not null) previous.Opacity = session.Page > 0 ? 0.9 : 0.25;
        if (next is not null) next.Opacity = session.Page + 1 < session.PageCount ? 0.9 : 0.25;
        if (hint is not null) hint.Text = selected is null ? "保持按住中键 · 移到窗口后松开切换 · Esc 取消" : "松开中键切换到选中窗口";
    }
    private void UpdateThumbnails()
    {
        if (session is null) return;
        foreach (var (index, thumbnail) in thumbnails.ToArray())
            if (!thumbnail.Show(session.Layout.Image(index % session.Layout.Capacity), placement.Scale))
            { thumbnail.Dispose(); thumbnails.Remove(index); }
            else fallbacks[index].Visibility = Visibility.Collapsed;
    }
    private void ReleaseThumbnails()
    {
        foreach (var thumbnail in thumbnails.Values) thumbnail.Dispose();
        thumbnails.Clear();
    }
    public void HidePreview()
    {
        IsOpen = false; armed = false; pageDirection = 0;
        OverlayWindow.Hide(hwnd);
        ReleaseThumbnails();
        session = null;
        cards.Clear(); statuses.Clear(); fallbacks.Clear(); canvas.Children.Clear();
    }
    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(ColorHelper.FromArgb(255, r, g, b));

#if DEBUG
    internal int ThumbnailCountForSmoke => thumbnails.Count;
    internal int PageForSmoke => session?.Page ?? -1;
    internal (int X, int Y) NextPagePointForSmoke => (
        placement.Left + (int)((session!.Layout.Next.X + 30) * placement.Scale),
        placement.Top + (int)((session.Layout.Next.Y + 16) * placement.Scale));
    internal (int X, int Y) CardPointForSmoke(int index)
    {
        var rect = session!.Layout.Card(index);
        return (placement.Left + (int)((rect.X + rect.Width / 2) * placement.Scale),
            placement.Top + (int)((rect.Y + rect.Height / 2) * placement.Scale));
    }
#endif
}