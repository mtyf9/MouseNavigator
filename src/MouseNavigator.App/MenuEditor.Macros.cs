using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using MouseNavigator.Windows;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    private Action? macroStopAction;
    private async Task EditMacroAsync()
    {
        if(dialogOpen||Entry is not {} entry)return;
        StopRecording();dialogOpen=true;
        var targetProfile=profileId;
        var steps=entry.Macro?.Steps.Select(s=>s with{Keys=s.Keys.ToArray()}).ToList()??[];
        var name=new TextBox{Header="宏名称",Text=entry.Macro?.Name??"自定义宏",MaxLength=80};
        var list=new ListView{Height=280,SelectionMode=ListViewSelectionMode.Single};
        var status=new TextBlock{Text="每次完整按下并松开组合键，会添加一个步骤。录入不会执行操作。",TextWrapping=TextWrapping.Wrap};
        var capture=new Button{Content="开始录入"};
        ShortcutRecorder? recorder=null;int generation=0;bool capturing=false;
        var timeout=new DispatcherTimer{Interval=TimeSpan.FromSeconds(60)};
        void Stop()
        {
            generation++;capturing=false;timeout.Stop();recorder?.Dispose();recorder=null;capture.Content="开始录入";
        }
        macroStopAction=Stop;timeout.Tick+=(_,_)=>{Stop();status.Text="录入已超时停止，已有步骤保留。";};
        void Render()
        {
            var selected=list.SelectedIndex;list.Items.Clear();
            for(var i=0;i<steps.Count;i++)
            {
                var index=i;var row=new StackPanel{Orientation=Orientation.Horizontal,Spacing=12};
                row.Children.Add(new TextBlock{Text=$"{i+1}. {ShortcutKeys.Format(steps[i].Keys)}",Width=180,VerticalAlignment=VerticalAlignment.Center});
                var delay=new NumberBox{Header="执行前等待（毫秒）",Value=steps[i].DelayMilliseconds,Minimum=0,Maximum=60000,Width=170,SmallChange=50,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact};
                delay.ValueChanged+=(_,_)=>{if(double.IsFinite(delay.Value)&&index<steps.Count)steps[index]=steps[index] with{DelayMilliseconds=(int)Math.Clamp(delay.Value,0,60000)};};
                row.Children.Add(delay);list.Items.Add(row);
            }
            list.SelectedIndex=steps.Count==0?-1:Math.Clamp(selected,0,steps.Count-1);
        }
        void Begin(int? replace=null)
        {
            Stop();var session=generation;
            try
            {
                capturing=true;capture.Content="停止录入";
                status.Text="录入中：连续输入组合键；Esc、停止录入或切换窗口结束。每步默认等待 200 毫秒，可修改。";
                recorder=new ShortcutRecorder(OwnerWindowHandle,keys=>DispatcherQueue.TryEnqueue(()=>
                {
                    if(!capturing||generation!=session)return;
                    if(keys is null){Stop();return;}
                    if(replace is int index){steps[index]=steps[index] with{Keys=keys.ToArray()};Stop();}
                    else if(steps.Count<100)steps.Add(new(keys.ToArray(),200));
                    if(steps.Count>=100){Stop();status.Text="已达到 100 步上限。";}
                    Render();
                }),continuous:replace is null);
                timeout.Start();
            }
            catch(Exception ex){Stop();status.Text="录入失败："+ex.Message;}
        }
        capture.Click+=(_,_)=>{if(capturing)Stop();else Begin();};
        var tools=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};
        tools.Children.Add(capture);
        tools.Children.Add(MakeButton("重新录入选中步",(_,_)=>{if(list.SelectedIndex>=0)Begin(list.SelectedIndex);}));
        void Move(int direction)
        {
            Stop();var index=list.SelectedIndex;var target=index+direction;
            if(index<0||target<0||target>=steps.Count)return;
            var step=steps[index];steps.RemoveAt(index);steps.Insert(target,step);Render();list.SelectedIndex=target;
        }
        tools.Children.Add(MakeButton("上移",(_,_)=>Move(-1)));
        tools.Children.Add(MakeButton("下移",(_,_)=>Move(1)));
        tools.Children.Add(MakeButton("删除",(_,_)=>{Stop();if(list.SelectedIndex<0)return;steps.RemoveAt(list.SelectedIndex);Render();}));
        var body=new StackPanel{Spacing=12,MinWidth=480};
        body.Children.Add(name);body.Children.Add(tools);body.Children.Add(status);body.Children.Add(list);
        body.Children.Add(new TextBlock{Text="执行时按 Esc 可停止。切换到其他应用后自动停止。宏仅录入键盘组合，不包含鼠标轨迹。",TextWrapping=TextWrapping.Wrap,Opacity=0.7});
        Render();
        var dialog=new ContentDialog{Title="配置宏操作",Content=body,PrimaryButtonText="确定",CloseButtonText="取消",XamlRoot=XamlRoot};
        dialog.PrimaryButtonClick+=(_,args)=>{
            Stop();
            try{MacroValidation.Validate(new(name.Text,steps));}
            catch(ArgumentException ex){args.Cancel=true;status.Text=ex.Message;}
        };
        try
        {
            if(await ShowDialogAsync(dialog)!=ContentDialogResult.Primary)return;
            draft.SetMacro(targetProfile,entry.Id,new(name.Text.Trim(),steps.ToArray()));MarkDirty();RefreshButton();RefreshPresets();
        }
        finally{Stop();macroStopAction=null;dialogOpen=false;}
    }
}