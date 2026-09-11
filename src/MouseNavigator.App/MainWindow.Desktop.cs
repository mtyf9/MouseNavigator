using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using MouseNavigator.Windows;
namespace MouseNavigator.App;

public sealed partial class MainWindow
{
    private TrayIcon? tray;
    private StartupRegistration? startup;
    private bool startupLoading=true, homeOperation;
    internal bool CanHideToTray => tray?.Registered==true;
    private void InitializeDesktopIntegration()
    {
        RefreshStartupState();
        try
        {
            var iconPath=Path.Combine(AppContext.BaseDirectory,"Assets","MouseNavigator.ico");
            AppWindow.SetIcon(iconPath);
            tray=new(WinRT.Interop.WindowNative.GetWindowHandle(this),iconPath,
                command=>DispatcherQueue.TryEnqueue(()=>HandleTrayCommand(command)));
            tray.Update(EnabledSwitch.IsOn,EnabledSwitch.IsEnabled&&controller is not null);
        }
        catch(Exception ex){ShowHomeStatus("托盘初始化失败","关闭窗口将退出程序。"+ex.Message,InfoBarSeverity.Warning);}
    }
    private void RefreshStartupState()
    {
        if(StartupSwitch is null)return;
        startupLoading=true;
        try
        {
            startup??=new(Environment.ProcessPath??throw new InvalidOperationException("无法确定程序路径。"));
            StartupSwitch.IsOn=startup.Enabled;StartupSwitch.IsEnabled=true;
            StartupHint.Text=StartupSwitch.IsOn?"已设置：登录 Windows 后驻留托盘。":"登录 Windows 后自动运行，启动后驻留托盘。";
        }
        catch(Exception ex){StartupSwitch.IsEnabled=false;StartupHint.Text="无法读取开机启动设置："+ex.Message;}
        finally{startupLoading=false;}
    }
    private void StartupSwitch_Toggled(object sender,RoutedEventArgs e)
    {
        if(startupLoading||startup is null)return;
        try
        {
            startup.SetEnabled(StartupSwitch.IsOn);
            StartupHint.Text=StartupSwitch.IsOn?"已设置：登录 Windows 后驻留托盘。":"已关闭开机启动。";
        }
        catch(Exception ex){RefreshStartupState();ShowHomeStatus("开机启动设置失败",ex.Message,InfoBarSeverity.Error);}
    }
    private void HandleTrayCommand(TrayCommand command)
    {
        switch(command)
        {
            case TrayCommand.Open: ShowMainWindow();break;
            case TrayCommand.Toggle:
                if(EnabledSwitch.IsEnabled)EnabledSwitch.IsOn=!EnabledSwitch.IsOn;
                break;
            case TrayCommand.Exit:
                if(homeOperation||editor.HasOpenDialog||!editor.IsEnabled){ShowMainWindow();return;}
                _=RequestExitAsync();break;
        }
    }
    private string TrayHintPreferencePath=>Path.Combine(Path.GetDirectoryName(store.FilePath)!,"tray-close-hint.dismissed");
#if DEBUG
    internal Func<ContentDialog,Task>? TrayHintOpenedForSmoke {get;set;}
#endif
    private async Task HideToTrayAsync()
    {
        if(homeOperation||closeDialogOpen||editor.HasOpenDialog||!editor.IsEnabled)return;
        closeDialogOpen=true;editor.StopRecording();
        try
        {
            if(!File.Exists(TrayHintPreferencePath))
            {
                var remember=new CheckBox{Content="不再提示"};
                var content=new StackPanel{Spacing=16};
                content.Children.Add(new TextBlock{Text="关闭窗口后，程序会最小化到任务栏右下角的托盘图标，并继续在后台运行。\n\n点击托盘图标可重新打开主界面；右键图标选择“退出”可结束程序。",TextWrapping=TextWrapping.Wrap});
                content.Children.Add(remember);
                var dialog=new ContentDialog
                {
                    XamlRoot=Content.XamlRoot,RequestedTheme=ElementTheme.Dark,Title="最小化到托盘",
                    Content=content,PrimaryButtonText="最小化到托盘",CloseButtonText="取消",
                    DefaultButton=ContentDialogButton.Primary
                };
                var operation=dialog.ShowAsync();
#if DEBUG
                if(TrayHintOpenedForSmoke is not null)await TrayHintOpenedForSmoke(dialog);
#endif
                if(await operation!=ContentDialogResult.Primary)return;
                if(!CanHideToTray){ShowHomeStatus("暂时无法隐藏","托盘图标不可用，请稍后重试。",InfoBarSeverity.Warning);return;}
                if(remember.IsChecked==true)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(TrayHintPreferencePath)!);
                    File.WriteAllText(TrayHintPreferencePath,"1");
                }
            }
            if(CanHideToTray)AppWindow.Hide();
        }
        catch(Exception ex){ShowHomeStatus("无法最小化到托盘",ex.Message,InfoBarSeverity.Error);}
        finally{closeDialogOpen=false;}
    }
    private async Task RequestExitAsync()
    {
        if(homeOperation||closeDialogOpen||editor.HasOpenDialog||!editor.IsEnabled)return;
        closeDialogOpen=true;
        try
        {
            if(editor.IsDirty)
            {
                ShowMainWindow();
                var dialog=new ContentDialog
                {
                    XamlRoot=Content.XamlRoot,RequestedTheme=ElementTheme.Dark,Title="保存菜单修改？",
                    Content="当前菜单有未保存的修改。",
                    PrimaryButtonText="保存并退出",SecondaryButtonText="放弃并退出",CloseButtonText="继续编辑",
                    DefaultButton=ContentDialogButton.Primary
                };
                var result=await dialog.ShowAsync();
                if(result==ContentDialogResult.None)return;
                if(result==ContentDialogResult.Primary)await editor.SaveDraftAsync();
            }
            closeApproved=true;Close();
        }
        catch(Exception ex){ShowHomeStatus("退出失败",ex.Message,InfoBarSeverity.Error);}
        finally{closeDialogOpen=false;}
    }
    internal void ShowMainWindow()
    {
        AppWindow.Show();Activate();
        TrayIcon.BringToFront(WinRT.Interop.WindowNative.GetWindowHandle(this));
    }
    private async void BackupButton_Click(object sender,RoutedEventArgs e)=>
        await RunHomeOperationAsync(()=>ExportConfigurationAsync(editor.ConfigurationForBackup));
    private async void RestoreButton_Click(object sender,RoutedEventArgs e)=>
        await RunHomeOperationAsync(async()=>
        {
            var configuration=await ImportConfigurationAsync();if(configuration is null)return;
            var dialog=new ContentDialog
            {
                XamlRoot=Content.XamlRoot,RequestedTheme=ElementTheme.Dark,Title="还原配置？",
                Content=$"将使用备份中的 {configuration.Profiles.Count} 个菜单替换现有全部配置。当前未保存的编辑也会被替换，还原后立即生效。",
                PrimaryButtonText="还原配置",CloseButtonText="取消",DefaultButton=ContentDialogButton.Close
            };
            if(await dialog.ShowAsync()!=ContentDialogResult.Primary)return;
            await ApplyRestoredConfigurationAsync(configuration);
            ShowHomeStatus("还原完成","全部菜单和预设已还原并应用。",InfoBarSeverity.Success);
        });
    private async Task ApplyRestoredConfigurationAsync(NavigatorConfiguration configuration)
    {
        // Validate and save before replacing the editor's saved baseline.
        ConfigurationCodec.Validate(configuration);
        await SaveConfigurationAsync(configuration);
        editor.AcceptRestoredConfiguration(configuration);
    }
    private async Task RunHomeOperationAsync(Func<Task> operation)
    {
        if(homeOperation||closeDialogOpen||editor.HasOpenDialog||!editor.IsEnabled)return;
        homeOperation=true;BackupButton.IsEnabled=RestoreButton.IsEnabled=false;editor.IsEnabled=false;editor.StopRecording();
        try{await operation();}
        catch(Exception ex){ShowHomeStatus("配置操作失败",ex.Message,InfoBarSeverity.Error);}
        finally{homeOperation=false;BackupButton.IsEnabled=RestoreButton.IsEnabled=true;editor.IsEnabled=true;}
    }
    private void ShowHomeStatus(string title,string message,InfoBarSeverity severity)
    {
        StatusBar.Title=title;StatusBar.Message=message;StatusBar.Severity=severity;StatusBar.IsOpen=true;
    }
}