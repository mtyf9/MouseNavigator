using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace MouseNavigator.App;
public sealed partial class MainWindow
{
    private FrameworkElement CreateBlacklistSettings()
    {
        var names=new TextBox{Header="应用进程（分号分隔）",PlaceholderText="例如 game.exe; editor.exe",Text=string.Join("; ",editor.CurrentBlockedApplications),AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=68};
        var panel=new StackPanel{Spacing=10,Children={new TextBlock{Text="应用黑名单",FontSize=20},new TextBlock{Text="鼠标位于这些应用时不触发悬浮窗，保留原按键操作。黑名单优先于所有菜单的应用匹配。",TextWrapping=TextWrapping.Wrap},names}};
        string[] Values()=>names.Text.Split([';','；',',','\r','\n'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var browse=new Button{Content="选择应用程序…"};
        readSettingsBlacklist=Values;names.TextChanged+=(_,_)=>RefreshSettingsDirty();
        var status=new TextBlock{TextWrapping=TextWrapping.Wrap};
        browse.Click+=async(_,_)=>await RunHomeOperationAsync(async()=>{
            var selected=await PickProgramsAsync();
            if(selected is not null)names.Text=string.Join("; ",Values().Concat(selected).Distinct(StringComparer.OrdinalIgnoreCase));
        });
        panel.Children.Add(new StackPanel{Orientation=Orientation.Horizontal,Spacing=8,Children={browse}});
        panel.Children.Add(status);return panel;
    }
}
