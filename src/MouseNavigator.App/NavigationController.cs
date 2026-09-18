using System.Collections.Concurrent;
using Microsoft.UI.Dispatching;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using MouseNavigator.Windows;
namespace MouseNavigator.App;

internal sealed class NavigationController : IDisposable
{
    private readonly DispatcherQueue dispatcher;
    private readonly IWindowCatalog catalog;
    private ActionRegistry registry;
    private ProfileResolver resolver;
    private readonly RingWindow ring;
    private readonly MiddleMouseHook hook;
    private readonly Func<WindowIdentity, ActionResult> activateWindow;
    private WindowPreviewWindow? preview;
    private bool hasInteractiveAction;
    private long previewRefreshAt, previewEntryAt;
    private string? pendingPreview;
    private int pointerX, pointerY;
    private readonly DispatcherQueueTimer escapeTimer;
    private readonly ConcurrentQueue<MouseSample> queue = new();
    private int scheduled;
    private bool active, visible, busy, disposed;
    private CancellationTokenSource? execution;
    private nint macroTarget;
    private int originX, originY;
    private double scale;
    private ApplicationContext? context;
    private MenuProfile? menu;
    private TriggerSettings trigger=new();
    private long heldSince;
    public void ConfigureTrigger(TriggerSettings value){Cancel();trigger=value;hook.Configure(value);while(queue.TryDequeue(out _)){} }
    public event Action<string>? ContextChanged;
    public event Action<ActionResult>? Completed;

    public NavigationController(DispatcherQueue dispatcher, IWindowCatalog catalog, ActionRegistry registry, ProfileResolver resolver,
        Func<WindowIdentity, ActionResult>? activateWindow = null)
    {
        this.dispatcher = dispatcher; this.catalog = catalog; this.registry = registry; this.resolver = resolver;
        this.activateWindow = activateWindow ?? new PlatformActions(catalog).ActivateWindow;
        ring = new RingWindow();
        try { hook = new MiddleMouseHook(); }
        catch { ring.Close(); throw; }
        hook.Input += OnInput;
        escapeTimer = dispatcher.CreateTimer();
        escapeTimer.Interval = TimeSpan.FromMilliseconds(80);
        escapeTimer.Tick += (_, _) =>
        {
            if(busy&&(MiddleMouseHook.EscapePressed||(macroTarget!=0&&WindowCatalog.Foreground!=macroTarget)))execution?.Cancel();
            if (!active) return;
            try
            {
                // The hook consumes middle input. Only actual Up/Cancel ends a held gesture.
                if (MiddleMouseHook.EscapePressed) { Cancel(); return; }
                if(!visible&&preview?.IsOpen!=true&&menu?.Enabled==true&&trigger.Mode==TriggerMode.Hold&&Environment.TickCount64-heldSince>=trigger.HoldMilliseconds)
                {ring.ShowAt(originX,originY);hook.ClickWindow=ring.Handle;visible=true;Handle(new(MousePhase.Move,pointerX,pointerY,0));}
                if (visible && pendingPreview is not null && Environment.TickCount64 >= previewEntryAt
                    && ring.HitTest(pointerX,pointerY)==pendingPreview) OpenPreview(pointerX,pointerY);
                if (preview?.IsOpen == true)
                {
                    if (Environment.TickCount64 >= previewRefreshAt)
                    {
                        preview.RefreshAvailable(catalog.Enumerate());
                        previewRefreshAt = Environment.TickCount64 + 300;
                    }
                    preview.Tick();
                }
            }
            catch (Exception ex) { Cancel(); Completed?.Invoke(ActionResult.Failure(ex.Message)); }
        };
        escapeTimer.Start();
    }
    public bool Enabled
    {
        get => hook.Enabled;
        set { hook.Enabled = value; if (!value){execution?.Cancel();Cancel();} }
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
        // Interactive ring entries need every crossing; preview cards only need the final position.
        while (true)
        {
            while (queue.TryDequeue(out var sample))
            {
                while ((preview?.IsOpen == true || (visible && !hasInteractiveAction)) && sample.Phase == MousePhase.Move && queue.TryPeek(out var next) && next.Phase == MousePhase.Move)
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
        if(sample.Phase==MousePhase.PointerDown)
        {
            if(active&&trigger.Mode==TriggerMode.Toggle&&!(preview?.IsOpen==true?preview.ContainsPoint(sample.X,sample.Y):visible&&ring.ContainsPoint(sample.X,sample.Y)))Cancel();
            return;
        }
        var leftClick=sample.Phase==MousePhase.LeftClick;
        if(leftClick){if(!active||trigger.Mode!=TriggerMode.Toggle)return;sample=sample with{Phase=MousePhase.Up};}
        if(trigger.Mode==TriggerMode.Toggle)
        {
            if(sample.Phase==MousePhase.Up&&!leftClick)return;
            if(sample.Phase==MousePhase.Down&&active)sample=sample with{Phase=MousePhase.Up};
        }
        if (sample.Phase == MousePhase.Down)
        {
            Cancel();
            active = true;hook.Tracking=true;heldSince=Environment.TickCount64;
            originX=pointerX=sample.X;originY=pointerY=sample.Y;
            scale = OverlayWindow.ScaleAt(sample.X, sample.Y);
            context = catalog.Capture(sample.PointerWindow!=0?sample.PointerWindow:sample.Foreground);
            menu = resolver.Resolve(context);
            if (!menu.Enabled) return; // Keep the held click until Up so ordinary middle clicks still replay.
            hasInteractiveAction = menu.Entries.Any(e => registry.Find(e.ActionId)?.Descriptor.Interaction == ActionInteraction.WindowPreview);
            ring.SetMenu(menu, registry,trigger.Mode==TriggerMode.Toggle);
            ContextChanged?.Invoke($"{context.ProcessName} · {menu.Name}");
            if(trigger.Mode==TriggerMode.Toggle){ring.ShowAt(originX,originY);hook.ClickWindow=ring.Handle;visible=true;}
            return;
        }
        if (!active) return;
        if (MiddleMouseHook.EscapePressed) { Cancel(); return; }
        if (preview?.IsOpen == true)
        {
            if (sample.Phase == MousePhase.Move) preview.MovePointer(sample.X, sample.Y);
            if (sample.Phase == MousePhase.Up)
            {
                preview.RefreshAvailable(catalog.Enumerate());
                var selectedWindow = leftClick?preview.ClickSelectionAt(sample.X,sample.Y):preview.SelectionAt(sample.X, sample.Y);
                Cancel();
                if (selectedWindow is not null) Completed?.Invoke(activateWindow(selectedWindow.Identity));
            }
            return;
        }
        if (sample.Phase == MousePhase.Move)
        {
            if (menu?.Enabled == false) return;
            pointerX=sample.X;pointerY=sample.Y;
            if (!visible && Environment.TickCount64-heldSince>=trigger.HoldMilliseconds && Math.Sqrt(Math.Pow(sample.X - originX, 2) + Math.Pow(sample.Y - originY, 2)) >= 10 * scale)
            {
                ring.ShowAt(originX, originY);hook.ClickWindow=ring.Handle; visible = true;
            }
            if (visible)
            {
                var slot = ring.HitTest(sample.X, sample.Y);
                ring.Highlight(slot); pointerX=sample.X;pointerY=sample.Y;
                if(pendingPreview!=slot)pendingPreview=null;
                var entry = menu!.Entries.FirstOrDefault(e => e.Id == slot);
                if (entry is not null && registry.Find(entry.ActionId)?.Descriptor.Interaction == ActionInteraction.WindowPreview)
                {
                    if (menu.RingCount <= 1) OpenPreview(sample.X,sample.Y);
                    else if(pendingPreview!=entry.Id) {pendingPreview=entry.Id;previewEntryAt=Environment.TickCount64+250;}                }
            }
        }
        if (sample.Phase == MousePhase.Up)
        {
            var wasVisible = visible;
            var selected = visible ? ring.HitTest(sample.X, sample.Y) : null;
            var invocation = context!;
            var entry = menu!.Entries.FirstOrDefault(e => e.Id == selected);
            if(leftClick&&entry is not null&&registry.Find(entry.ActionId)?.Descriptor.Interaction==ActionInteraction.WindowPreview){OpenPreview(sample.X,sample.Y);return;}
            Cancel();
            if (!wasVisible)
            {
                if (WindowCatalog.WindowAt(sample.X,sample.Y) == invocation.WindowHandle && !PlatformActions.ReplayTrigger(trigger))
                    Completed?.Invoke(ActionResult.Failure("原触发按键未能传递。"));
            }
            else if (entry is not null && entry.ActionId.Length != 0) Execute(entry.ActionId, invocation with { Launch=entry.Launch, Macro=entry.Macro });
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
    internal WindowPreviewWindow? PreviewForSmoke => preview;
#endif
    private async void Execute(string id, ApplicationContext invocation)
    {
        busy = true;execution=new();macroTarget=0;
        try
        {
            if(invocation.Macro is not null)
            {
                if(!PlatformActions.FocusActionTarget(invocation.WindowHandle)){Completed?.Invoke(ActionResult.Failure("无法激活鼠标下方的目标窗口，宏已取消。"));return;}
                macroTarget=invocation.WindowHandle;
            }
            var result = await registry.ExecuteAsync(id, invocation,execution.Token);
            if (!disposed) Completed?.Invoke(result);
        }
        finally { busy = false;macroTarget=0;execution?.Dispose();execution=null; }
    }
    private void OpenPreview(int x,int y)
    {
        pendingPreview=null;
        preview ??= new WindowPreviewWindow();
        preview.ShowAt(x,y,catalog.Enumerate(),menu?.PreviewAppearance,trigger.Mode==TriggerMode.Toggle);previewRefreshAt=Environment.TickCount64+300;
        hook.ClickWindow=preview.Handle;ring.HideRing();visible=false;
    }
    private void Cancel() { hook.ClickWindow=0;hook.Tracking=false;pendingPreview=null; active = false; visible = false; ring.HideRing(); preview?.HidePreview(); }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;execution?.Cancel();
        escapeTimer.Stop();
        hook.Input -= OnInput;
        hook.Dispose();
        Cancel();
        preview?.Close();
        ring.Close();
    }
}
