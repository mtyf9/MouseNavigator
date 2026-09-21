using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MouseNavigator.Windows;
namespace MouseNavigator.App;
public sealed partial class MainWindow
{
    private Window? settingsWindow;
    private ContentControl? settingsRoot;
    private Func<MouseNavigator.Contracts.TriggerSettings>? readSettingsTrigger;
    private Func<string[]>? readSettingsBlacklist;
    private Button? settingsApply;
    private bool settingsCloseApproved,settingsPromptOpen;
    private bool SettingsDirty=>readSettingsTrigger is not null&&readSettingsBlacklist is not null
        &&(readSettingsTrigger()!=editor.CurrentTrigger||!readSettingsBlacklist().SequenceEqual(editor.CurrentBlockedApplications,StringComparer.OrdinalIgnoreCase));
    private void RefreshSettingsDirty(){if(settingsApply is not null)settingsApply.IsEnabled=SettingsDirty&&triggerRecorder is null;}
    private async Task<bool> ApplyGlobalSettingsAsync()
    {
        StopTriggerRecording();
        try{await editor.SaveGlobalSettingsAsync(readSettingsTrigger!(),readSettingsBlacklist!());RefreshSettingsDirty();return true;}
        catch(Exception ex){await new ContentDialog{XamlRoot=ConfigurationDialogRoot,Title="无法保存全局设置",Content=ex.Message,CloseButtonText="返回修改"}.ShowAsync();return false;}
    }
    private async Task ConfirmSettingsCloseAsync()
    {
        if(settingsPromptOpen)return;settingsPromptOpen=true;StopTriggerRecording();
        try
        {
            var result=await new ContentDialog{XamlRoot=ConfigurationDialogRoot,Title="保存全局设置？",Content="全局设置有未应用的修改。",PrimaryButtonText="保存并关闭",SecondaryButtonText="放弃修改",CloseButtonText="继续编辑"}.ShowAsync();
            if(result==ContentDialogResult.None)return;
            if(result==ContentDialogResult.Primary&&!await ApplyGlobalSettingsAsync())return;
            settingsCloseApproved=true;settingsWindow?.Close();
        }
        finally{settingsPromptOpen=false;}
    }
    private XamlRoot ConfigurationDialogRoot=>settingsRoot?.XamlRoot??Content.XamlRoot;
    private void GlobalSettings_Click(object sender,RoutedEventArgs e)=>ShowSettingsWindow();
    private void ShowSettingsWindow()
    {
        if(settingsWindow is not null){settingsWindow.Activate();return;}
        if(homeOperation||closeDialogOpen||editor.HasOpenDialog)return;
        var window=new Window{Title="MouseNavigator 全局设置",SystemBackdrop=new MicaBackdrop()};
        var root=new ContentControl{HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch,Padding=new Thickness(24),RequestedTheme=ElementTheme.Dark,Background=new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255,32,32,32))};
        settingsCloseApproved=false;var body=new StackPanel{Spacing=16,Margin=new Thickness(0,0,16,0)};
        body.Children.Add(new TextBlock{Text="全局设置",FontSize=28});
        body.Children.Add(CreateTriggerSettings());body.Children.Add(CreateBlacklistSettings());
        body.Children.Add(new TextBlock{Text="配置备份",FontSize=20});
        body.Children.Add(new TextBlock{Text="备份包含全部菜单、预设按钮、图标和宏。",TextWrapping=TextWrapping.Wrap});
        var backups=new StackPanel{Orientation=Orientation.Horizontal,Spacing=12};
        var backup=new Button{Content="备份配置"};var restore=new Button{Content="还原配置"};
        backup.Click+=BackupButton_Click;restore.Click+=RestoreButton_Click;
        backups.Children.Add(backup);backups.Children.Add(restore);body.Children.Add(backups);
        body.Children.Add(new TextBlock{Text="恢复默认",FontSize=20,Margin=new Thickness(0,16,0,0)});
        body.Children.Add(new TextBlock{Text="重置全部悬浮窗菜单，保留个人预设按钮。此操作会覆盖未保存的菜单修改。",TextWrapping=TextWrapping.Wrap});
        var reset=new Button{Content="恢复默认菜单"};
        reset.Click+=async(_,_)=>await RunHomeOperationAsync(async()=>{
            var dialog=new ContentDialog{XamlRoot=ConfigurationDialogRoot,Title="恢复全部默认菜单？",
                Content="所有自建菜单和菜单修改将被默认菜单替换，并立即保存生效。个人预设按钮保留。建议先备份配置。",
                PrimaryButtonText="确认恢复",CloseButtonText="取消",DefaultButton=ContentDialogButton.Close};
            if(await dialog.ShowAsync()!=ContentDialogResult.Primary)return;
            await editor.ResetMenusAsync();ShowHomeStatus("恢复完成","已恢复全部默认菜单。",InfoBarSeverity.Success);
        });
        body.Children.Add(reset);
        var layout=new Grid{RowSpacing=20};layout.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});layout.RowDefinitions.Add(new(){Height=GridLength.Auto});
        layout.Children.Add(new ScrollViewer{Content=body,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,HorizontalContentAlignment=HorizontalAlignment.Stretch});
        var footer=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10,HorizontalAlignment=HorizontalAlignment.Right};
        var accept=new Button{Content="确定"};var cancel=new Button{Content="取消"};settingsApply=new Button{Content="应用",IsEnabled=false};
        async Task SaveAndClose(){if(homeOperation||settingsPromptOpen)return;homeOperation=true;try{if(await ApplyGlobalSettingsAsync()){settingsCloseApproved=true;window.Close();}}finally{homeOperation=false;}}
        accept.Click+=async(_,_)=>await SaveAndClose();
        cancel.Click+=(_,_)=>{if(homeOperation||settingsPromptOpen)return;settingsCloseApproved=true;window.Close();};
        settingsApply.Click+=async(_,_)=>{if(homeOperation||settingsPromptOpen)return;homeOperation=true;try{await ApplyGlobalSettingsAsync();}finally{homeOperation=false;}};
        footer.Children.Add(accept);footer.Children.Add(cancel);footer.Children.Add(settingsApply);Grid.SetRow(footer,1);layout.Children.Add(footer);
        root.Content=layout;window.Content=root;
        settingsWindow=window;settingsRoot=root;
        var scale=OverlayWindow.WindowScale(WinRT.Interop.WindowNative.GetWindowHandle(this));
        window.AppWindow.Resize(new((int)(620*scale),(int)(720*scale)));
        window.AppWindow.Move(new(AppWindow.Position.X+60,AppWindow.Position.Y+60));
        window.AppWindow.Closing+=(sender,e)=>{if(settingsCloseApproved)return;if(homeOperation||settingsPromptOpen){e.Cancel=true;return;}if(SettingsDirty){e.Cancel=true;_=ConfirmSettingsCloseAsync();}};
        window.Activated+=(_,args)=>{if(args.WindowActivationState==WindowActivationState.Deactivated)StopTriggerRecording();};
        window.Closed+=(_,_)=>{StopTriggerRecording();settingsWindow=null;settingsRoot=null;readSettingsTrigger=null;readSettingsBlacklist=null;settingsApply=null;};
        window.Activate();
    }
}