using System.ComponentModel;
using System.Runtime.InteropServices;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using static MouseNavigator.Windows.NativeMethods;
namespace MouseNavigator.Windows;
public sealed class TriggerRecorder:IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct KeyData{public uint Key,Scan,Flags,Time;public nuint Extra;}
    private readonly Thread thread;
    private readonly ManualResetEventSlim ready=new();
    private readonly HookProc mouseCallback,keyCallback;
    private readonly Action<TriggerSettings?> completed;
    private readonly nint owner;
    private nint mouseHook,keyHook;
    private uint threadId;
    private Exception? error;
    private TriggerSettings? pending;
    private bool cancelPending,finished,disposed;
    public TriggerRecorder(nint owner,Action<TriggerSettings?> completed)
    {
        this.owner=owner;this.completed=completed;
        mouseCallback=Mouse;keyCallback=Keyboard;
        thread=new Thread(Pump){IsBackground=true,Name="MouseNavigator.TriggerRecorder"};thread.Start();ready.Wait();
        if(error is not null){ready.Dispose();throw error;}
    }
    private void Pump()
    {
        try
        {
            threadId=GetCurrentThreadId();PeekMessageW(out _,0,0,0,0);
            mouseHook=SetWindowsHookExW(14,mouseCallback,GetModuleHandleW(null),0);
            keyHook=SetWindowsHookExW(13,keyCallback,GetModuleHandleW(null),0);
            if(mouseHook==0||keyHook==0)throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        catch(Exception ex){error=ex;}
        finally{ready.Set();}
        try{if(error is null)while(GetMessageW(out var msg,0,0,0)>0){TranslateMessage(ref msg);DispatchMessageW(ref msg);}}
        finally{if(mouseHook!=0)UnhookWindowsHookEx(mouseHook);if(keyHook!=0)UnhookWindowsHookEx(keyHook);}
    }
    private bool Accept(TriggerDevice device,ushort key,bool down)
    {
        if(finished)return false;
        if(GetForegroundWindow()!=owner){finished=true;completed(null);return false;}
        if(pending is null)
        {
            if(!down)return false;
            if(device==TriggerDevice.Keyboard&&key!=27&&!ShortcutKeys.MainKeys.ContainsKey(key))return false;
            pending=new(Device:device,Key:key);cancelPending=device==TriggerDevice.Keyboard&&key==27;
        }
        if(pending.Device!=device||(device==TriggerDevice.Keyboard&&pending.Key!=key))return false;
        if(!down){finished=true;completed(cancelPending?null:pending);}
        return true;
    }
    private nint Mouse(int code,nint message,nint data)
    {
        try
        {
            if(code==0)
            {
                var p=Marshal.PtrToStructure<MouseData>(data);var id=message.ToInt32();
                if((p.Flags&1)==0&&(id is 0x207 or 0x208 or 0x20B or 0x20C))
                {
                    var device=id is 0x207 or 0x208?TriggerDevice.MiddleMouse:(p.Data>>16)==1?TriggerDevice.MouseX1:TriggerDevice.MouseX2;
                    if(Accept(device,119,id is 0x207 or 0x20B))return 1;
                }
            }
        }
        catch{finished=true;}
        return CallNextHookEx(mouseHook,code,message,data);
    }
    private nint Keyboard(int code,nint message,nint data)
    {
        try
        {
            if(code==0)
            {
                var p=Marshal.PtrToStructure<KeyData>(data);var id=message.ToInt32();
                if((p.Flags&0x10)==0&&(id is 0x100 or 0x101 or 0x104 or 0x105)&&Accept(TriggerDevice.Keyboard,(ushort)p.Key,id is 0x100 or 0x104))return 1;
            }
        }
        catch{finished=true;}
        return CallNextHookEx(keyHook,code,message,data);
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;finished=true;
        PostThreadMessageW(threadId,0x12,0,0);thread.Join();ready.Dispose();
    }
}