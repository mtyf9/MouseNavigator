using MouseNavigator.Contracts;
namespace MouseNavigator.Actions.BuiltIn;
public sealed class WindowsPlugin : INavigatorPlugin
{
    public string Id => "windows";
    public int ApiVersion => 1;
    public IReadOnlyList<INavigatorAction> CreateActions(IPlatformActions platform)
    {
        INavigatorAction Shortcut(string id, string name, string glyph, params ushort[] keys) =>
            new Action(new("windows." + id, name, glyph, name), c => platform.SendShortcut(c.WindowHandle, keys));
        return [
            new Action(new("windows.window.previous", "上一个窗口", "\uE76B", "按稳定顺序向左切换窗口"), c => platform.SwitchWindow(c.WindowHandle, WindowDirection.Previous)),
            new Action(new("windows.window.next", "下一个窗口", "\uE76C", "按稳定顺序向右切换窗口"), c => platform.SwitchWindow(c.WindowHandle, WindowDirection.Next)),
            Shortcut("tasks", "任务视图", "\uE7C4", 0x5B, 0x09),
            Shortcut("minimizeAll", "全部最小化", "\uE921", 0x5B, 0x4D),
            Shortcut("maximize", "最大化窗口", "\uE922", 0x5B, 0x26),
            Shortcut("desktop.previous", "上一个桌面", "\uE892", 0x11, 0x5B, 0x25),
            Shortcut("desktop.next", "下一个桌面", "\uE893", 0x11, 0x5B, 0x27)
        ];
    }
    private sealed class Action(ActionDescriptor descriptor, Func<ApplicationContext, ActionResult> execute) : INavigatorAction
    {
        public ActionDescriptor Descriptor => descriptor;
        public ValueTask<ActionResult> ExecuteAsync(ApplicationContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(execute(context));
        }
    }
}
