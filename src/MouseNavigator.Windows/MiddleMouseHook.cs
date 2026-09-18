using System.ComponentModel;
using MouseNavigator.Contracts;
using System.Runtime.InteropServices;
using static MouseNavigator.Windows.NativeMethods;
namespace MouseNavigator.Windows;

public enum MousePhase { Down, Move, Up, Cancel, PointerDown, LeftClick }
public readonly record struct MouseSample(MousePhase Phase, int X, int Y, nint Foreground, nint PointerWindow = 0);

/// <summary>Dedicated message pump keeps WinUI rendering and plugin work outside the low-level callback.</summary>
public sealed class MiddleMouseHook : IDisposable
{
    private readonly Thread thread;
    private readonly ManualResetEventSlim ready = new();
    private readonly HookProc callback;
    private nint hook,keyboardHook;
    private readonly HookProc keyboardCallback;
    [StructLayout(LayoutKind.Sequential)] private struct KeyData {public uint Key,Scan,Flags,Time;public nuint Extra;}
    private TriggerSettings settings=new();
    private TriggerSettings? pressed;
    public bool Tracking {get=>tracking;set=>tracking=value;}
    private volatile bool tracking;
    public void Configure(TriggerSettings value)=>Volatile.Write(ref settings,value);
    private uint threadId;
    private Exception? startupError;
    private bool held,leftHeld;
    private nint clickWindow;
    public nint ClickWindow{get=>Interlocked.CompareExchange(ref clickWindow,0,0);set=>Interlocked.Exchange(ref clickWindow,value);}
    private volatile bool enabled = true;
    private bool disposed;
    public event Action<MouseSample>? Input;
    public bool Enabled { get => enabled; set => enabled = value; }

    public MiddleMouseHook(bool initiallyEnabled=true)
    {
        enabled=initiallyEnabled;
        callback = OnMouse;keyboardCallback=OnKeyboard;
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
            keyboardHook=SetWindowsHookExW(13,keyboardCallback,GetModuleHandleW(null),0);
            if(keyboardHook==0)throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        catch (Exception ex) { startupError = ex;if(hook!=0)UnhookWindowsHookEx(hook);if(keyboardHook!=0)UnhookWindowsHookEx(keyboardHook); }
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
        finally { UnhookWindowsHookEx(hook);UnhookWindowsHookEx(keyboardHook); hook = keyboardHook = 0; }
    }

    private nint OnMouse(int code, nint message, nint data)
    {
        if (code != 0) return CallNextHookEx(hook, code, message, data);
        var sample = Marshal.PtrToStructure<MouseData>(data);
        if ((sample.Flags & 1) != 0) return CallNextHookEx(hook, code, message, data);
        var id = message.ToInt32();
        try
        {
            if(id==0x202&&leftHeld){leftHeld=false;Publish(enabled?MousePhase.LeftClick:MousePhase.Cancel,sample.Point.X,sample.Point.Y);return 1;}
            if(id==0x201&&tracking&&Volatile.Read(ref settings).Mode==TriggerMode.Toggle&&ClickWindow!=0&&WindowCatalog.WindowAt(sample.Point.X,sample.Point.Y)==ClickWindow)
            {leftHeld=true;return 1;}
            var configured=Volatile.Read(ref settings);
            var target=pressed??configured;
            bool Matches(TriggerSettings t)=>t.Device switch{
                TriggerDevice.MiddleMouse=>id is 0x207 or 0x208,
                TriggerDevice.MouseX1=>(id is 0x20B or 0x20C)&&(sample.Data>>16)==1,
                TriggerDevice.MouseX2=>(id is 0x20B or 0x20C)&&(sample.Data>>16)==2,
                _=>false};
            if(id==0x200&&(held||tracking))Input?.Invoke(new(MousePhase.Move,sample.Point.X,sample.Point.Y,0));
            if(tracking&&configured.Mode==TriggerMode.Toggle&&(id is 0x201 or 0x204 or 0x207 or 0x20B)&&!Matches(target))
                Publish(MousePhase.PointerDown,sample.Point.X,sample.Point.Y);
            if(Matches(target))
            {
                if((id is 0x207 or 0x20B)&&enabled&&!held&&(tracking||MayBegin(sample.Point.X,sample.Point.Y)))
                {held=true;pressed=configured;Publish(MousePhase.Down,sample.Point.X,sample.Point.Y);return 1;}
                if((id is 0x208 or 0x20C)&&held)
                {held=false;pressed=null;Publish(enabled?MousePhase.Up:MousePhase.Cancel,sample.Point.X,sample.Point.Y);return 1;}
            }
        }
        catch
        {
            // Never unwind a managed exception through a native hook callback.
            held = false;pressed=null;
        }
        return CallNextHookEx(hook, code, message, data);
    }

    private static bool MayBegin(int x,int y)
    {
        GetWindowThreadProcessId(WindowCatalog.WindowAt(x,y),out var pid);
        return pid!=Environment.ProcessId;
    }
    private void Publish(MousePhase phase,int x,int y)=>Input?.Invoke(new(phase,x,y,
        phase==MousePhase.Down?GetForegroundWindow():0,phase==MousePhase.Down?WindowCatalog.WindowAt(x,y):0));
    private nint OnKeyboard(int code,nint message,nint data)
    {
        if(code!=0)return CallNextHookEx(keyboardHook,code,message,data);
        try
        {
            var key=Marshal.PtrToStructure<KeyData>(data);var id=message.ToInt32();
            var configured=Volatile.Read(ref settings);var target=pressed??configured;
            if((key.Flags&0x10)!=0||target.Device!=TriggerDevice.Keyboard||key.Key!=target.Key)
                return CallNextHookEx(keyboardHook,code,message,data);
            if(id is 0x100 or 0x104)
            {
                if(held)return 1;
                GetCursorPos(out var cursor);
                if(enabled&&(tracking||MayBegin(cursor.X,cursor.Y)))
                {held=true;pressed=configured;GetCursorPos(out var p);Publish(MousePhase.Down,p.X,p.Y);return 1;}
            }
            else if(id is 0x101 or 0x105 && held)
            {held=false;pressed=null;GetCursorPos(out var p);Publish(enabled?MousePhase.Up:MousePhase.Cancel,p.X,p.Y);return 1;}
        }
        catch {held=false;pressed=null;}
        return CallNextHookEx(keyboardHook,code,message,data);
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
