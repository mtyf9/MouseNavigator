using System.Runtime.InteropServices;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using static MouseNavigator.Windows.NativeMethods;
namespace MouseNavigator.Windows;
public sealed class PlatformActions(IWindowCatalog catalog) : IPlatformActions
{
    private readonly WindowCycle cycle = new();
    public ActionResult SwitchWindow(nint source, WindowDirection direction)
    {
        var windows = catalog.Enumerate();
        // A modal dialog is represented by its owner in the navigation list.
        if (!windows.Any(w => w.Identity.Handle == source))
        {
            var visited = new HashSet<nint> { source };
            for (var owner = GetWindow(source, 4); owner != 0 && visited.Add(owner); owner = GetWindow(owner, 4))
            {
                if (!windows.Any(w => w.Identity.Handle == owner)) continue;
                source = owner;
                break;
            }
        }
        var selected = cycle.Select(windows, source, direction);
        if (selected is null) return ActionResult.Failure("当前桌面没有其他可切换的窗口。");
        GetWindowThreadProcessId(selected.Identity.Handle, out var pid);
        if (!IsWindow(selected.Identity.Handle) || pid != selected.Identity.ProcessId)
            return ActionResult.Failure("目标窗口已关闭，请重试。");
        var hwnd = selected.Identity.Handle;
        var popup = GetLastActivePopup(hwnd);
        if (popup != 0 && IsWindowVisible(popup)) hwnd = popup;
        if (IsIconic(selected.Identity.Handle)) ShowWindowAsync(selected.Identity.Handle, 9);
        return SetForegroundWindow(hwnd)
            ? ActionResult.Success($"已切换到 {selected.Title}")
            : ActionResult.Failure("Windows 未允许激活目标窗口，请重新触发手势。");
    }
    public ActionResult SendShortcut(nint source, params ushort[] keys)
    {
        if (!IsWindow(source)) return ActionResult.Failure("原应用窗口已关闭。");
        if (GetForegroundWindow() != source && !SetForegroundWindow(source))
            return ActionResult.Failure("无法激活原应用，快捷键已取消。");
        if (new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C }.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0))
            return ActionResult.Failure("请松开键盘修饰键后重试。");
        var inputs = keys.Select(k => Key(k, false)).Concat(keys.Reverse().Select(k => Key(k, true))).ToArray();
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent == inputs.Length) return ActionResult.Success("操作已执行。");
        var release = keys.Reverse().Select(k => Key(k, true)).ToArray();
        SendInput((uint)release.Length, release, Marshal.SizeOf<Input>());
        return ActionResult.Failure("快捷键发送失败，目标应用可能以更高权限运行。");
    }
    private static Input Key(ushort key, bool up) => new()
    {
        Type = 1,
        Value = new() { Keyboard = new() { Key = key, Flags = (up ? 2u : 0u) | (key is >= 0x21 and <= 0x28 or 0x2D or 0x2E or 0x5B or 0x5C ? 1u : 0u) } }
    };
    public static bool ReplayMiddleClick()
    {
        Input[] inputs = [new() { Value = new() { Mouse = new() { Flags = 0x20 } } }, new() { Value = new() { Mouse = new() { Flags = 0x40 } } }];
        return SendInput(2, inputs, Marshal.SizeOf<Input>()) == 2;
    }
}
