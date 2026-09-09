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
            new Action(new("windows.window.preview", "窗口预览", "\uE7C4", "保持按住中键，移到窗口缩略图后松开切换", ActionInteraction.WindowPreview),
                _ => ActionResult.Failure("请保持按住中键，移入窗口预览区域后选择窗口。")),
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
