using System.Runtime.InteropServices;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using static MouseNavigator.Windows.NativeMethods;
namespace MouseNavigator.Windows;
public sealed class PlatformActions(IWindowCatalog catalog) : IPlatformActions
{
    public ActionResult ActivateWindow(WindowIdentity identity)
    {
        // Re-enumerate on commit: a closed/reused window or one moved to another desktop is invalid.
        var selected = catalog.Enumerate().FirstOrDefault(w => w.Identity == identity);
        if (selected is null) return ActionResult.Failure("目标窗口已关闭或不在当前桌面，请重新选择。");
        GetWindowThreadProcessId(identity.Handle, out var pid);
        if (!IsWindow(identity.Handle) || pid != identity.ProcessId)
            return ActionResult.Failure("目标窗口已关闭，请重试。");
        var hwnd = identity.Handle;
        var popup = GetLastActivePopup(hwnd);
        if (popup != 0 && IsWindowVisible(popup)) hwnd = popup;
        if (IsIconic(identity.Handle)) ShowWindowAsync(identity.Handle, 9);
        return SetForegroundWindow(hwnd)
            ? ActionResult.Success($"已切换到 {selected.Title}")
            : ActionResult.Failure("Windows 未允许激活目标窗口，请重新触发手势。");
    }
    public static bool FocusActionTarget(nint source)=>IsWindow(source)&&(GetForegroundWindow()==source||SetForegroundWindow(source));
    public bool IsMacroTargetActive(nint source)=>IsWindow(source)&&GetForegroundWindow()==source;
    public ActionResult LaunchApplication(ApplicationLaunch target)
    {
        try
        {
            ConfigurationCodec.ValidateLaunch(target);
            if(!File.Exists(target.ExecutablePath))return ActionResult.Failure("程序不存在，请重新选择应用程序。");
            using var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName=target.ExecutablePath,Arguments=target.Arguments,
                WorkingDirectory=Path.GetDirectoryName(target.ExecutablePath)!,UseShellExecute=false
            });
            return process is null?ActionResult.Failure("程序未能启动。"):ActionResult.Success("已启动应用程序。");
        }
        catch(Exception ex) when(ex is ArgumentException or IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {return ActionResult.Failure("启动应用失败："+ex.Message);}
    }
    public ActionResult ToggleMaximize(nint source)
    {
        if(!IsWindow(source))return ActionResult.Failure("目标窗口已关闭。");
        var restore=IsZoomed(source);
        if(!restore&&(GetWindowLongPtrW(source,-16).ToInt64()&0x10000)==0)
            return ActionResult.Failure("此窗口不支持最大化。");
        if(!ShowWindowAsync(source,restore?9:3))return ActionResult.Failure("无法改变窗口状态。");
        return ActionResult.Success(restore?"已还原窗口。":"已最大化窗口。");
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
        Value = new() { Keyboard = new() { Key = key, Flags = (up ? 2u : 0u) | (key is >= 0x21 and <= 0x28 or 0x2D or 0x2E or 0x5B or 0x5C or >= 0xAD and <= 0xB3 ? 1u : 0u) } }
    };
    public static bool ReplayTrigger(TriggerSettings trigger)
    {
        if(trigger.Device==TriggerDevice.MiddleMouse)return ReplayMiddleClick();
        Input[] inputs=trigger.Device==TriggerDevice.Keyboard?[Key(trigger.Key,false),Key(trigger.Key,true)]:
            [new(){Value=new(){Mouse=new(){Flags=0x80,Data=trigger.Device==TriggerDevice.MouseX1?1u:2u}}},
             new(){Value=new(){Mouse=new(){Flags=0x100,Data=trigger.Device==TriggerDevice.MouseX1?1u:2u}}}];
        return SendInput(2,inputs,Marshal.SizeOf<Input>())==2;
    }
    public static bool ReplayMiddleClick()
    {
        Input[] inputs = [new() { Value = new() { Mouse = new() { Flags = 0x20 } } }, new() { Value = new() { Mouse = new() { Flags = 0x40 } } }];
        return SendInput(2, inputs, Marshal.SizeOf<Input>()) == 2;
    }
}
