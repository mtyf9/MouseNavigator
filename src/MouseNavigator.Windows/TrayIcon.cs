using System.ComponentModel;
using System.Runtime.InteropServices;
namespace MouseNavigator.Windows;

public enum TrayCommand { Open = 1, Toggle = 2, Exit = 3 }

/// <summary>UI-thread-owned notification icon; recreates itself after Explorer restarts.</summary>
public sealed class TrayIcon : IDisposable
{
    private const uint CallbackMessage = 0x8000 + 71;
    private const nuint SubclassId = 0x4D4E;
    private readonly nint hwnd;
    private readonly nint icon;
    private readonly SubclassProc callback;
    private readonly Action<TrayCommand> dispatch;
    private readonly uint taskbarCreated = RegisterWindowMessageW("TaskbarCreated");
    private static readonly uint ShowMessage = RegisterWindowMessageW("MouseNavigator.ShowMainWindow");
    private bool disposed, enabled = true, canToggle = true;
    public bool Registered { get; private set; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct IconData
    {
        public uint Size;
        public nint Window;
        public uint Id, Flags, Message;
        public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Title;
        public uint InfoFlags;
        public Guid Guid;
        public nint BalloonIcon;
    }
    private delegate nint SubclassProc(nint window, uint message, nuint wParam, nint lParam, nuint id, nuint data);
    public TrayIcon(nint window, string iconPath, Action<TrayCommand> dispatch)
    {
        hwnd = window; this.dispatch = dispatch; callback = WindowProc;
        icon = LoadImageW(0, iconPath, 1, 32, 32, 0x10);
        if (icon == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "无法加载托盘图标。");
        if (!SetWindowSubclass(hwnd, callback, SubclassId, 0))
        {
            DestroyIcon(icon); throw new InvalidOperationException("无法注册托盘事件。");
        }
        if (!Register())
        {
            Dispose(); throw new InvalidOperationException("无法添加托盘图标。");
        }
    }
    private IconData Data() => new()
    {
        Size = (uint)Marshal.SizeOf<IconData>(), Window = hwnd, Id = 1, Icon = icon,
        Flags = 1 | 2 | 4 | 0x80, Message = CallbackMessage,
        Tip = enabled ? "MouseNavigator · 已启用" : "MouseNavigator · 已暂停",
        Info = "", Title = ""
    };
    private bool Register()
    {
        var data = Data();
        Registered = Shell_NotifyIconW(0, ref data);
        if (Registered) { data.Version = 4; Shell_NotifyIconW(4, ref data); }
        return Registered;
    }
    public void Update(bool isEnabled, bool allowToggle)
    {
        enabled = isEnabled; canToggle = allowToggle;
        if (disposed) return;
        if (!Registered) { Register(); return; }
        var data = Data(); Shell_NotifyIconW(1, ref data);
    }
    private nint WindowProc(nint window, uint message, nuint wParam, nint lParam, nuint id, nuint data)
    {
        // Never propagate a managed exception through the native window procedure.
        try
        {
            if (!disposed && message == taskbarCreated) { Registered = false; Register(); }
            else if (!disposed && message == ShowMessage) dispatch(TrayCommand.Open);
            else if (!disposed && message == CallbackMessage)
            {
                var notification = (uint)((long)lParam & 0xffff);
                if (notification is 0x400 or 0x401 or 0x203) dispatch(TrayCommand.Open);
                else if (notification == 0x7b) ShowMenu();
                return 0;
            }
        }
        catch { /* Keep the WinUI message loop alive if the shell is shutting down. */ }
        return DefSubclassProc(window, message, wParam, lParam);
    }
    private void ShowMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == 0) return;
        try
        {
            AppendMenuW(menu, 0, 1, "打开主界面");
            AppendMenuW(menu, canToggle ? 0u : 1u, 2, enabled ? "暂停悬浮导航" : "启用悬浮导航");
            AppendMenuW(menu, 0x800, 0, null);
            AppendMenuW(menu, 0, 3, "退出");
            NativeMethods.GetCursorPos(out var point);
            NativeMethods.SetForegroundWindow(hwnd);
            var command = TrackPopupMenuEx(menu, 0x100 | 0x2, point.X, point.Y, hwnd, 0);
            PostMessageW(hwnd, 0, 0, 0);
            if (command is >= 1 and <= 3) dispatch((TrayCommand)command);
        }
        finally { DestroyMenu(menu); }
    }
    public static void RequestShow() => SendNotifyMessageW((nint)0xffff, ShowMessage, 0, 0);
    public static void BringToFront(nint window)
    {
        NativeMethods.ShowWindow(window, 9);
        NativeMethods.SetForegroundWindow(window);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        var data = Data(); Shell_NotifyIconW(2, ref data); Registered = false;
        RemoveWindowSubclass(hwnd, callback, SubclassId);
        DestroyIcon(icon);
        GC.KeepAlive(callback);
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIconW(uint command, ref IconData data);
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(nint hwnd, SubclassProc callback, nuint id, nuint data);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(nint hwnd, SubclassProc callback, nuint id);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint hwnd, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint LoadImageW(nint instance, string path, uint type, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessageW(string name);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenuW(nint menu, uint flags, nuint id, string? text);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint owner, nint parameters);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] private static extern bool PostMessageW(nint hwnd, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern bool SendNotifyMessageW(nint hwnd, uint message, nuint wParam, nint lParam);
}