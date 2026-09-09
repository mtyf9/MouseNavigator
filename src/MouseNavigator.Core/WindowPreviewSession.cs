namespace MouseNavigator.Core;

public readonly record struct PreviewRect(double X, double Y, double Width, double Height)
{
    public bool Contains(double x, double y) => x >= X && y >= Y && x < X + Width && y < Y + Height;
}

/// <summary>One immutable ordering per held gesture. Closed/reused windows never move other cards.</summary>
public sealed class WindowPreviewSession
{
    private readonly WindowCandidate[] windows;
    private HashSet<WindowIdentity> available;
    public IReadOnlyList<WindowCandidate> Windows => windows;
    public int Page { get; private set; }
    public int PageCount => Math.Max(1, (windows.Length + Layout.Capacity - 1) / Layout.Capacity);
    public WindowPreviewLayout Layout { get; }
    public WindowPreviewSession(IEnumerable<WindowCandidate> windows, WindowPreviewLayout layout)
    {
        this.windows = windows.DistinctBy(w => w.Identity).ToArray();
        available = this.windows.Select(w => w.Identity).ToHashSet();
        Layout = layout;
    }
    public IEnumerable<(int Index, WindowCandidate Window)> Visible =>
        windows.Select((window, index) => (index, window)).Skip(Page * Layout.Capacity).Take(Layout.Capacity);
    public bool IsAvailable(WindowIdentity id) => available.Contains(id);
    public void RefreshAvailable(IEnumerable<WindowCandidate> current) => available = current.Select(w => w.Identity).ToHashSet();
    public WindowCandidate? HitTest(double x, double y)
    {
        foreach (var (index, window) in Visible)
            if (IsAvailable(window.Identity) && Layout.Card(index % Layout.Capacity).Contains(x, y)) return window;
        return null;
    }
    public bool TurnPage(int direction)
    {
        var next = Math.Clamp(Page + Math.Sign(direction), 0, PageCount - 1);
        if (next == Page) return false;
        Page = next;
        return true;
    }
}

public sealed class WindowPreviewLayout
{
    public double Width { get; }
    public double Height { get; }
    public int Columns { get; }
    public int Rows { get; }
    public int Capacity => Columns * Rows;
    private readonly double cardWidth, cardHeight;
    public PreviewRect Previous => new(16, Height - 48, 112, 32);
    public PreviewRect Next => new(Width - 128, Height - 48, 112, 32);
    public WindowPreviewLayout(double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width < 240 || height < 240)
            throw new ArgumentException("预览区域过小。");
        Width = width; Height = height;
        Columns = Math.Clamp((int)((width - 20) / 232), 1, 4);
        cardWidth = (width - 32 - (Columns - 1) * 12) / Columns;
        cardHeight = Math.Min(cardWidth * 0.56 + 52, height - 136);
        Rows = Math.Clamp((int)((height - 124) / (cardHeight + 12)), 1, 3);
    }
    public PreviewRect Card(int slot) => new(16 + slot % Columns * (cardWidth + 12),
        72 + slot / Columns * (cardHeight + 12), cardWidth, cardHeight);
    public PreviewRect Image(int slot)
    {
        var card = Card(slot);
        return new(card.X + 6, card.Y + 6, card.Width - 12, card.Height - 50);
    }
}