using System.ComponentModel;
using System.Runtime.InteropServices;
using static MouseNavigator.Windows.NativeMethods;
namespace MouseNavigator.Windows;

public enum MousePhase { Down, Move, Up, Cancel }
public readonly record struct MouseSample(MousePhase Phase, int X, int Y, nint Foreground);

/// <summary>Dedicated message pump keeps WinUI rendering and plugin work outside the low-level callback.</summary>
public sealed class MiddleMouseHook : IDisposable
{
    private readonly Thread thread;
    private readonly ManualResetEventSlim ready = new();
    private readonly HookProc callback;
    private nint hook;
    private uint threadId;
    private Exception? startupError;
    private bool held;
    private volatile bool enabled = true;
    private bool disposed;
    public event Action<MouseSample>? Input;
    public bool Enabled { get => enabled; set => enabled = value; }

    public MiddleMouseHook()
    {
        callback = OnMouse;
        thread = new Thread(Pump) { IsBackground = true, Name = "MouseNavigator.Input" };
        thread.Start();
        ready.Wait();
        if (startupError is not null) { ready.Dispose(); throw startupError; }
    }

    private void Pump()
    {
        try
        {
            threadId = GetCurrentThreadId();
            PeekMessageW(out _, 0, 0, 0, 0);
            hook = SetWindowsHookExW(14, callback, GetModuleHandleW(null), 0);
            if (hook == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        catch (Exception ex) { startupError = ex; }
        finally { ready.Set(); }
        if (startupError is not null) return;
        try
        {
            while (GetMessageW(out var message, 0, 0, 0) > 0)
            {
                TranslateMessage(ref message);
                DispatchMessageW(ref message);
            }
        }
        finally { UnhookWindowsHookEx(hook); hook = 0; }
    }

    private nint OnMouse(int code, nint message, nint data)
    {
        if (code != 0) return CallNextHookEx(hook, code, message, data);
        var sample = Marshal.PtrToStructure<MouseData>(data);
        if ((sample.Flags & 1) != 0) return CallNextHookEx(hook, code, message, data);
        var id = message.ToInt32();
        try
        {
            if (id == 0x207 && enabled)
            {
                held = true;
                Input?.Invoke(new(MousePhase.Down, sample.Point.X, sample.Point.Y, GetForegroundWindow()));
                return 1;
            }
            if (held && id == 0x200)
                Input?.Invoke(new(MousePhase.Move, sample.Point.X, sample.Point.Y, 0));
            if (held && id == 0x208)
            {
                held = false;
                Input?.Invoke(new(enabled ? MousePhase.Up : MousePhase.Cancel, sample.Point.X, sample.Point.Y, 0));
                return 1;
            }
        }
        catch
        {
            // Never unwind a managed exception through a native hook callback.
            held = false;
        }
        return CallNextHookEx(hook, code, message, data);
    }

    public static bool EscapePressed => (GetAsyncKeyState(0x1B) & 0x8000) != 0;


    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        enabled = false;
        PostThreadMessageW(threadId, 0x12, 0, 0);
        thread.Join();
        ready.Dispose();
    }
}
