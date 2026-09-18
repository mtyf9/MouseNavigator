using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using MouseNavigator.Windows;
namespace MouseNavigator.App;
public sealed partial class MainWindow
{
    private TriggerRecorder? triggerRecorder;
    private Action? finishTriggerRecording;
    private int triggerRecordingVersion;
    private void StopTriggerRecording()
    {
        triggerRecordingVersion++;triggerRecorder?.Dispose();triggerRecorder=null;
        var finish=finishTriggerRecording;finishTriggerRecording=null;finish?.Invoke();
    }
    private FrameworkElement CreateTriggerSettings()
    {
        var current=editor.CurrentTrigger;var selected=current;
        var panel=new StackPanel{Spacing=10};
        panel.Children.Add(new TextBlock{Text="悬浮窗触发",FontSize=20});
        var mode=new ComboBox{Header="触发方式",HorizontalAlignment=HorizontalAlignment.Stretch};
        mode.Items.Add("长按：按住选择，松开执行");mode.Items.Add("点按：按一次打开，左键或再按一次执行");
        mode.SelectedIndex=(int)current.Mode;
        string Label(TriggerSettings value)=>value.Device switch{
            TriggerDevice.MiddleMouse=>"鼠标中键",TriggerDevice.MouseX1=>"鼠标侧键 1（后退）",
            TriggerDevice.MouseX2=>"鼠标侧键 2（前进）",_=>ShortcutKeys.MainKeys.GetValueOrDefault(value.Key,"键盘键")};
        var keyText=new TextBlock{Text="当前触发键："+Label(selected)};
        var record=new Button{Content="录入触发键"};
        var delay=new Slider{Header="长按时间（毫秒）",Minimum=100,Maximum=2000,StepFrequency=50,Value=current.HoldMilliseconds};
        void Refresh()=>delay.Visibility=mode.SelectedIndex==0?Visibility.Visible:Visibility.Collapsed;
        mode.SelectionChanged+=(_,_)=>Refresh();Refresh();
        var status=new TextBlock{TextWrapping=TextWrapping.Wrap};
        var apply=new Button{Content="应用触发设置"};
        record.Click+=(_,_)=>{
            if(triggerRecorder is not null){StopTriggerRecording();status.Text="已取消录入。";return;}
            var wasEnabled=controller?.Enabled==true;
            if(controller is not null)controller.Enabled=false;
            record.Content="取消录入";apply.IsEnabled=false;status.Text="请按下并松开中键、侧键或一个键盘键；Esc 取消。";
            var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(15)};
            finishTriggerRecording=()=>{
                timer.Stop();record.Content="录入触发键";apply.IsEnabled=true;status.Text="录入已结束，未应用的设置不会生效。";
                if(controller is not null)controller.Enabled=wasEnabled&&EnabledSwitch.IsOn;
            };
            var version=++triggerRecordingVersion;
            timer.Tick+=(_,_)=>{StopTriggerRecording();status.Text="录入超时，请重试。";};
            try
            {
                triggerRecorder=new(WinRT.Interop.WindowNative.GetWindowHandle(settingsWindow!),value=>DispatcherQueue.TryEnqueue(()=>{
                    if(version!=triggerRecordingVersion)return;
                    StopTriggerRecording();
                    if(value is null){status.Text="已取消录入。";return;}
                    selected=selected with{Device=value.Device,Key=value.Key};
                    keyText.Text="当前触发键："+Label(selected);status.Text="已录入，点击应用触发设置后生效。";
                }));
                timer.Start();
            }
            catch(Exception ex){StopTriggerRecording();status.Text="录入失败："+ex.Message;}
        };
        panel.Children.Add(mode);panel.Children.Add(keyText);panel.Children.Add(record);panel.Children.Add(delay);
        panel.Children.Add(new TextBlock{Text="点按模式可左键点击按钮执行，点击悬浮窗外部关闭。长按未达到时间会传递原按键。支持中键、两个侧键和键盘单键，Esc 保留用于取消。",TextWrapping=TextWrapping.Wrap,FontSize=12,Opacity=.7});
        apply.Click+=async(_,_)=>await RunHomeOperationAsync(async()=>{
            await editor.SaveTriggerAsync(selected with{Mode=(TriggerMode)mode.SelectedIndex,HoldMilliseconds=(int)delay.Value});
            status.Text="触发设置已生效。";
        });
        panel.Children.Add(apply);panel.Children.Add(status);return panel;
    }
}