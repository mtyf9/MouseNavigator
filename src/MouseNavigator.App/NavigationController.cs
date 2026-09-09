using System.Collections.Concurrent;
using Microsoft.UI.Dispatching;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using MouseNavigator.Windows;
namespace MouseNavigator.App;

internal sealed class NavigationController : IDisposable
{
    private readonly DispatcherQueue dispatcher;
    private readonly WindowCatalog catalog;
    private ActionRegistry registry;
    private ProfileResolver resolver;
    private readonly RingWindow ring;
    private readonly MiddleMouseHook hook;
    private readonly DispatcherQueueTimer escapeTimer;
    private readonly ConcurrentQueue<MouseSample> queue = new();
    private int scheduled;
    private bool active, visible, busy, disposed;
    private int originX, originY;
    private double scale;
    private ApplicationContext? context;
    private MenuProfile? menu;
    public event Action<string>? ContextChanged;
    public event Action<ActionResult>? Completed;

    public NavigationController(DispatcherQueue dispatcher, WindowCatalog catalog, ActionRegistry registry, ProfileResolver resolver)
    {
        this.dispatcher = dispatcher; this.catalog = catalog; this.registry = registry; this.resolver = resolver;
        ring = new RingWindow();
        try { hook = new MiddleMouseHook(); }
        catch { ring.Close(); throw; }
        hook.Input += OnInput;
        escapeTimer = dispatcher.CreateTimer();
        escapeTimer.Interval = TimeSpan.FromMilliseconds(80);
        escapeTimer.Tick += (_, _) =>
        {
            if (!active) return;
            // The hook consumes middle-button input; asynchronous OS state is not the
            // gesture's release signal. Only the queued Up/Cancel ends a held gesture.
            if (MiddleMouseHook.EscapePressed) Cancel();
        };
        escapeTimer.Start();
    }
    public bool Enabled
    {
        get => hook.Enabled;
        set { hook.Enabled = value; if (!value) Cancel(); }
    }
    public void ApplyConfiguration(ActionRegistry updatedRegistry, ProfileResolver updatedResolver)
    {
        // This method and Drain run on the UI thread. No gesture may straddle two configurations.
        Cancel();
        while (queue.TryDequeue(out _)) { }
        registry = updatedRegistry;
        resolver = updatedResolver;
    }
    private void OnInput(MouseSample sample)
    {
        queue.Enqueue(sample);
        if (Interlocked.Exchange(ref scheduled, 1) == 0)
            if (!dispatcher.TryEnqueue(Drain)) Interlocked.Exchange(ref scheduled, 0);
    }
    private void Drain()
    {
        // Preserve every move until the ring opens: an outward-and-back batch must still trigger.
        // Once visible, only the latest position is needed for highlighting.
        while (true)
        {
            while (queue.TryDequeue(out var sample))
            {
                while (visible && sample.Phase == MousePhase.Move && queue.TryPeek(out var next) && next.Phase == MousePhase.Move)
                    queue.TryDequeue(out sample);
                if (!disposed)
                {
                    try { Handle(sample); }
                    catch (Exception ex) { Cancel(); Completed?.Invoke(ActionResult.Failure(ex.Message)); }
                }
            }
            Interlocked.Exchange(ref scheduled, 0);
            if (queue.IsEmpty || Interlocked.Exchange(ref scheduled, 1) != 0) break;
        }
    }
    private void Handle(MouseSample sample)
    {
        if (sample.Phase == MousePhase.Cancel) { Cancel(); return; }
        if (!Enabled || busy) return;
        if (sample.Phase == MousePhase.Down)
        {
            Cancel();
            active = true; originX = sample.X; originY = sample.Y;
            scale = OverlayWindow.ScaleAt(sample.X, sample.Y);
            context = catalog.Capture(sample.Foreground);
            menu = resolver.Resolve(context);
            ring.SetMenu(menu, registry);
            ContextChanged?.Invoke($"{context.ProcessName} · {menu.Name}");
            return;
        }
        if (!active) return;
        if (MiddleMouseHook.EscapePressed) { Cancel(); return; }
        if (sample.Phase == MousePhase.Move)
        {
            if (!visible && Math.Sqrt(Math.Pow(sample.X - originX, 2) + Math.Pow(sample.Y - originY, 2)) >= 10 * scale)
            {
                ring.ShowAt(originX, originY); visible = true;
            }
            if (visible) ring.Highlight(ring.HitTest(sample.X, sample.Y));
        }
        if (sample.Phase == MousePhase.Up)
        {
            var wasVisible = visible;
            var selected = visible ? ring.HitTest(sample.X, sample.Y) : null;
            var invocation = context!;
            var entry = menu!.Entries.FirstOrDefault(e => e.Slot == selected);
            Cancel();
            if (!wasVisible)
            {
                if (WindowCatalog.Foreground == invocation.WindowHandle && !PlatformActions.ReplayMiddleClick())
                    Completed?.Invoke(ActionResult.Failure("普通中键点击未能传递。"));
            }
            else if (entry is not null) Execute(entry.ActionId, invocation);
        }
    }
#if DEBUG
    // Exercise the real dispatcher, timer and HWND without injecting system mouse input.
    internal void QueueForSmoke(params MouseSample[] samples)
    {
        foreach (var sample in samples) OnInput(sample);
    }
    internal bool RingVisibleForSmoke => visible && ring.AppWindow.IsVisible;
    internal bool GestureActiveForSmoke => active;
#endif
    private async void Execute(string id, ApplicationContext invocation)
    {
        busy = true;
        try
        {
            var result = await registry.ExecuteAsync(id, invocation);
            if (!disposed) Completed?.Invoke(result);
        }
        finally { busy = false; }
    }
    private void Cancel() { active = false; visible = false; ring.HideRing(); }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        escapeTimer.Stop();
        hook.Input -= OnInput;
        hook.Dispose();
        Cancel();
        ring.Close();
    }
}
