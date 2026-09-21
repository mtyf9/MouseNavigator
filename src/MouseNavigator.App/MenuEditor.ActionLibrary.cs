using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MouseNavigator.Contracts;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    private readonly TextBlock selectedActionText=new(){TextWrapping=TextWrapping.Wrap,FontSize=12,Opacity=0.8};
    private readonly StackPanel launchPanel=new(){Spacing=8,Visibility=Visibility.Collapsed};
    private readonly TextBox launchPath=new(){Header="应用程序",IsReadOnly=true,PlaceholderText="请选择 .exe 程序"};
    private readonly TextBox launchArguments=new(){Header="启动参数（可选）",MaxLength=4096,PlaceholderText="由应用程序支持的参数"};
    public Func<Task<string?>>? PickApplicationRequested {get;set;}
    private async Task ChooseActionAsync(string id)
    {
        if(id=="windows.macro"){await EditMacroAsync();return;}
        if(id!="$shortcut"&&id!="windows.applications.launch"){SelectAction(id);return;}
        if(dialogOpen||Entry is null)return;
        var original=draft.Snapshot();var wasDirty=IsDirty;dialogOpen=true;bool accepted=false;
        ContentDialog? dialog=null;
        try
        {
            shortcutName.Header="快捷键名称";launchPath.Header="应用程序";SelectAction(id);
            var panel=id=="$shortcut"?shortcutPanel:launchPanel;
            panel.Visibility=Visibility.Visible;
            dialog=new ContentDialog{Title=id=="$shortcut"?"自定义快捷键":"启动应用程序",Content=panel,
                PrimaryButtonText="确定",CloseButtonText="取消",XamlRoot=XamlRoot};
            dialog.PrimaryButtonClick+=(_,args)=>{
                if(id=="$shortcut"&&string.IsNullOrWhiteSpace(shortcutName.Text)){args.Cancel=true;shortcutName.Header="请输入快捷键名称";}
                if(id=="windows.applications.launch"&&Entry?.Launch is null){args.Cancel=true;launchPath.Header="请先选择应用程序";}
            };
            accepted=await ShowDialogAsync(dialog)==ContentDialogResult.Primary;
        }
        catch(Exception ex){Notify(ex.Message,InfoBarSeverity.Error);}
        finally
        {
            StopRecording();if(dialog is not null)dialog.Content=null;dialogOpen=false;
            if(!accepted){draft=new(original);IsDirty=wasDirty;UpdateDirty();}
            RefreshButton();
        }
    }
    private void PopulateActionChoices()
    {
        // Collapsed rows can retain children while Parent reports null; detach from the owned rows first.
        foreach(var row in actionChoices.Children.OfType<Grid>())row.Children.Clear();

        if(selectedActionText.Parent is Grid oldSummary)oldSummary.Children.Clear();
        actionChoices.Children.Clear();
        actionChoices.Children.Add(new TextBlock{Text="选择动作",FontSize=16});
        actionChoices.Children.Add(selectedActionText);
        var previewActionRow=new Grid{ColumnSpacing=6};
        previewActionRow.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        previewActionRow.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        var selectPreview=MakeButton("窗口预览",(_,_)=>SelectAction("windows.window.preview"));
        selectPreview.HorizontalAlignment=HorizontalAlignment.Stretch;selectPreview.HorizontalContentAlignment=HorizontalAlignment.Left;selectPreview.MinHeight=40;
        previewActionRow.Children.Add(selectPreview);
        previewAppearanceButton.Visibility=Visibility.Visible;
        actionChoices.Children.Add(previewActionRow);
        foreach(var group in actions.Where(a=>a.Id!="windows.macro"&&a.Id!="windows.window.preview").GroupBy(a=>a.Category))
        {
            var category=new Button{Content=group.Key+"  ›",HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Left,MinHeight=40};
            var menu=new MenuFlyout();
            foreach(var action in group)
            {
                var item=new MenuFlyoutItem{Text=action.Name,Tag=action.Id,Icon=new FontIcon{Glyph=action.Glyph}};
                ToolTipService.SetToolTip(item,action.Description);
                item.Click+=async(_,_)=>{menu.Hide();await Task.Delay(100);await ChooseActionAsync(action.Id);};menu.Items.Add(item);
            }
            category.Flyout=menu;actionChoices.Children.Add(category);
        }
        var shortcut=MakeButton("自定义快捷键",async(_,_)=>await ChooseActionAsync("$shortcut"));
        shortcut.MinHeight=40;shortcut.HorizontalAlignment=HorizontalAlignment.Stretch;shortcut.HorizontalContentAlignment=HorizontalAlignment.Left;
        actionChoices.Children.Add(shortcut);
        var empty=MakeButton("不执行动作",(_,_)=>SelectAction(""));
        empty.MinHeight=40;empty.HorizontalAlignment=HorizontalAlignment.Stretch;empty.HorizontalContentAlignment=HorizontalAlignment.Left;
        actionChoices.Children.Add(empty);
        var macro=MakeButton("宏操作",async(_,_)=>await EditMacroAsync());
        macro.MinHeight=40;macro.HorizontalAlignment=HorizontalAlignment.Stretch;macro.HorizontalContentAlignment=HorizontalAlignment.Left;
        actionChoices.Children.Add(macro);
        RefreshActionSummary();
    }
    private void SelectAction(string id)
    {
        if(loading||Entry is not {} entry)return;
        try
        {
            if(id=="$shortcut")
            {
                if(draft.Shortcut(entry.ActionId)is null)draft.SetShortcut(profileId,entry.Id,"自定义快捷键",[17,75]);
            }
            else if(entry.ActionId!=id)draft.SetAction(profileId,entry.Id,id);
            MarkDirty();RefreshButton();
        }
        catch(Exception ex){Notify(ex.Message,InfoBarSeverity.Error);}
    }
    private void RefreshActionSummary()
    {
        var id=Entry?.ActionId??"";
        previewAppearanceButton.Visibility=Visibility.Visible;
        selectedActionText.Text="当前："+(Entry?.Macro?.Name??draft.Shortcut(id)?.Name??actions.FirstOrDefault(a=>a.Id==id)?.Name??
            (id.Length==0?"不执行动作":"不可用的动作："+id));
    }
    private void BuildLaunchControls()
    {
        launchPanel.Children.Add(launchPath);
        launchPanel.Children.Add(MakeButton("选择应用程序…",async(_,_)=>await RunAsync(async()=>
        {
            var entry=Entry;if(entry is null||entry.ActionId!="windows.applications.launch")return;
            var path=PickApplicationRequested is null?null:await PickApplicationRequested();if(path is null)return;
            draft.SetLaunch(profileId,entry.Id,new(path,entry.Launch?.Arguments??""));
            if(entry.Label is null)draft.SetAppearance(profileId,entry.Id,Path.GetFileNameWithoutExtension(path),entry.Glyph,entry.Image);
            MarkDirty();RefreshButton();
        })));
        launchPanel.Children.Add(launchArguments);
        launchPanel.Children.Add(new TextBlock{Text="选择后只保存配置，松开中键执行时才启动程序。",FontSize=11,Opacity=0.7,TextWrapping=TextWrapping.Wrap});
        launchArguments.TextChanged+=(_,_)=>{
            if(loading||Entry is not {ActionId:"windows.applications.launch",Launch:{} launch} entry||launch.Arguments==launchArguments.Text)return;
            try{draft.SetLaunch(profileId,entry.Id,launch with{Arguments=launchArguments.Text});MarkDirty();}
            catch(ArgumentException ex){Notify(ex.Message,InfoBarSeverity.Error);}
        };
    }
    private void RefreshLaunchFields()
    {
        var prior=loading;loading=true;
        try
        {
            var entry=Entry;
            launchPanel.Visibility=entry?.ActionId=="windows.applications.launch"?Visibility.Visible:Visibility.Collapsed;
            launchPath.Text=entry?.Launch?.ExecutablePath??"";
            launchArguments.Text=entry?.Launch?.Arguments??"";launchArguments.IsEnabled=entry?.Launch is not null;
        }
        finally{loading=prior;}
    }
}