#if DEBUG
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using MouseNavigator.Windows;
using MouseNavigator.Actions.BuiltIn;
namespace MouseNavigator.App;
public sealed partial class MainWindow
{
    internal async Task CheckTriggerAsync(List<string> results)
    {
        var fixture=new PointerCatalog(123);var fake=new PointerPlatform();var actions=new ActionRegistry();
        actions.Register(new WindowsPlugin(),fake);
        MenuProfile profile=new(3,"global","触发检查",[],[new("one","windows.editing.copy")],IsGlobalDefault:true);
        using var navigation=new NavigationController(DispatcherQueue,fixture,actions,new ProfileResolver([profile]));
        var x=AppWindow.Position.X+AppWindow.Size.Width/2;var y=AppWindow.Position.Y+AppWindow.Size.Height/2;
        var dx=(int)(100*OverlayWindow.ScaleAt(x,y));
        foreach(var device in Enum.GetValues<TriggerDevice>())
        {
            fake.Keys=null;
            navigation.ConfigureTrigger(new(TriggerMode.Toggle,device));
            navigation.QueueForSmoke(new MouseSample(MousePhase.Down,x,y,123,123),new MouseSample(MousePhase.Up,x,y,0));
            await Task.Delay(120);
            results.Add(navigation.RingVisibleForSmoke?"PASS: click remains open "+device:"FAIL: click open "+device);
            navigation.QueueForSmoke(new MouseSample(MousePhase.Move,x+dx,y,0),new MouseSample(MousePhase.Down,x+dx,y,123,123),new MouseSample(MousePhase.Up,x+dx,y,0));
            await Task.Delay(100);
            results.Add(!navigation.RingVisibleForSmoke&&fake.Keys?.SequenceEqual(new ushort[]{17,67})==true?"PASS: second click executes "+device:"FAIL: click execute "+device);
        }
        navigation.ConfigureTrigger(new(TriggerMode.Toggle));
        navigation.QueueForSmoke(new MouseSample(MousePhase.Down,x,y,123,123));await Task.Delay(100);
        navigation.QueueForSmoke(new MouseSample(MousePhase.PointerDown,x,y,0));await Task.Delay(100);
        results.Add(navigation.RingVisibleForSmoke?"PASS: inside click keeps menu":"FAIL: inside click");
        navigation.QueueForSmoke(new MouseSample(MousePhase.PointerDown,x+2000,y+2000,0));await Task.Delay(100);
        results.Add(!navigation.RingVisibleForSmoke&&!navigation.GestureActiveForSmoke?"PASS: outside click closes menu":"FAIL: outside click");
        fake.Keys=null;navigation.ConfigureTrigger(new(TriggerMode.Toggle));
        navigation.QueueForSmoke(new MouseSample(MousePhase.Down,x,y,123,123));await Task.Delay(100);
        navigation.QueueForSmoke(new MouseSample(MousePhase.LeftClick,x+dx,y,0));await Task.Delay(100);
        results.Add(!navigation.RingVisibleForSmoke&&fake.Keys?.SequenceEqual(new ushort[]{17,67})==true?"PASS: left click executes button":"FAIL: left click");
        navigation.ConfigureTrigger(new(HoldMilliseconds:400));
        navigation.QueueForSmoke(new MouseSample(MousePhase.Down,x,y,123,123));
        await Task.Delay(120);results.Add(!navigation.RingVisibleForSmoke?"PASS: hold waits for threshold":"FAIL: early hold");
        await Task.Delay(420);results.Add(navigation.RingVisibleForSmoke?"PASS: stationary hold opens":"FAIL: hold open");
        fake.Keys=null;
        navigation.QueueForSmoke(new MouseSample(MousePhase.Move,x+dx,y,0),new MouseSample(MousePhase.Up,x+dx,y,0));
        await Task.Delay(100);results.Add(fake.Keys is not null&&!navigation.RingVisibleForSmoke?"PASS: hold release executes":"FAIL: hold release");
        navigation.ConfigureTrigger(new(TriggerMode.Toggle));navigation.QueueForSmoke(new MouseSample(MousePhase.Down,x,y,123,123));await Task.Delay(100);
        navigation.Enabled=false;results.Add(!navigation.RingVisibleForSmoke?"PASS: disabling cancels click mode":"FAIL: disable");
    }
}
#endif