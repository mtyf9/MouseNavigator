using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MouseNavigator.Windows;
namespace MouseNavigator.App;
public sealed partial class MainWindow
{
    private Window? settingsWindow;
    private UserControl? settingsRoot;
    private XamlRoot ConfigurationDialogRoot=>settingsRoot?.XamlRoot??Content.XamlRoot;
    private void ShowSettingsWindow()
    {
        if(settingsWindow is not null){settingsWindow.Activate();return;}
        if(homeOperation||closeDialogOpen||editor.HasOpenDialog)return;
        var window=new Window{Title="MouseNavigator 设置",SystemBackdrop=new MicaBackdrop()};
        var root=new UserControl{Padding=new Thickness(24),RequestedTheme=ElementTheme.Dark};
        var body=new StackPanel{Spacing=16};
        body.Children.Add(new TextBlock{Text="设置",FontSize=28});
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
        root.Content=new ScrollViewer{Content=body};window.Content=root;
        settingsWindow=window;settingsRoot=root;
        var scale=OverlayWindow.WindowScale(WinRT.Interop.WindowNative.GetWindowHandle(this));
        window.AppWindow.Resize(new((int)(560*scale),(int)(480*scale)));
        window.AppWindow.Move(new(AppWindow.Position.X+60,AppWindow.Position.Y+60));
        window.AppWindow.Closing+=(_,e)=>{if(homeOperation)e.Cancel=true;};
        window.Closed+=(_,_)=>{settingsWindow=null;settingsRoot=null;};
        window.Activate();
    }
}