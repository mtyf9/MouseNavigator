using System.ComponentModel;
using System.Runtime.InteropServices;
using MouseNavigator.Core;
using static MouseNavigator.Windows.NativeMethods;
namespace MouseNavigator.Windows;
/// <summary>A short-lived hook on its own message pump, active only for the owning foreground window.</summary>
public sealed class ShortcutRecorder : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct KeyData {public uint Key,Scan,Flags,Time;public nuint Extra;}
    private readonly Thread thread;
    private readonly ManualResetEventSlim ready=new();
    private readonly HookProc callback;
    private readonly Action<ushort[]?> completed;
    private readonly nint owner;
    private readonly ShortcutRecording recording=new();
    private nint hook;
    private uint threadId;
    private Exception? startupError;
    private volatile bool finished;
    private bool disposed;
    public ShortcutRecorder(nint owner,Action<ushort[]?> completed)
    {
        this.owner=owner;this.completed=completed;callback=OnKeyboard;
        thread=new Thread(Pump){IsBackground=true,Name="MouseNavigator.ShortcutRecording"};thread.Start();ready.Wait();
        if(startupError is not null){ready.Dispose();throw startupError;}
    }
    private void Pump()
    {
        try
        {
            threadId=GetCurrentThreadId();PeekMessageW(out _,0,0,0,0);
            hook=SetWindowsHookExW(13,callback,GetModuleHandleW(null),0);
            if(hook==0)throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        catch(Exception ex){startupError=ex;}
        finally{ready.Set();}
        if(startupError is not null)return;
        try {while(GetMessageW(out var message,0,0,0)>0){TranslateMessage(ref message);DispatchMessageW(ref message);}}
        finally{UnhookWindowsHookEx(hook);hook=0;}
    }
    private nint OnKeyboard(int code,nint message,nint data)
    {
        if(code!=0||finished)return CallNextHookEx(hook,code,message,data);
        try
        {
            if(GetForegroundWindow()!=owner){finished=true;completed(null);return CallNextHookEx(hook,code,message,data);}
            var value=Marshal.PtrToStructure<KeyData>(data);
            if((value.Flags&0x10)!=0)return CallNextHookEx(hook,code,message,data);
            var id=message.ToInt32();
            if(id is not (0x100 or 0x101 or 0x104 or 0x105))return CallNextHookEx(hook,code,message,data);
            recording.Accept((ushort)value.Key,id is 0x100 or 0x104);
            if(recording.Completed){finished=true;completed(recording.Keys);}
            return 1;
        }
        catch {finished=true;return CallNextHookEx(hook,code,message,data);}
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;finished=true;
        PostThreadMessageW(threadId,0x12,0,0);
        if(Thread.CurrentThread!=thread)thread.Join(500);
        ready.Dispose();GC.KeepAlive(callback);
    }
}