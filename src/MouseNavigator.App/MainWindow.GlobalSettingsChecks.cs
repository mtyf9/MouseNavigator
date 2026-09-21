#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MouseNavigator.Contracts;
namespace MouseNavigator.App;
public sealed partial class MainWindow
{
    private static IEnumerable<FrameworkElement> SettingsElements(DependencyObject root)
    {
        if(root is FrameworkElement element)yield return element;
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
            foreach(var child in SettingsElements(VisualTreeHelper.GetChild(root,i)))yield return child;
    }
    internal async Task CheckGlobalSettingsAsync(List<string> results,string directory)
    {
        void Check(bool valid,string name)=>results.Add((valid?"PASS: ":"FAIL: ")+name);
        var before=editor.CurrentTrigger;var blocked=editor.CurrentBlockedApplications.ToArray();
        ShowSettingsWindow();await Task.Delay(180);
        readSettingsTrigger=()=>before with{HoldMilliseconds=before.HoldMilliseconds==500?600:500};
        readSettingsBlacklist=()=>new[]{"test.exe"};RefreshSettingsDirty();
        Check(SettingsDirty&&settingsApply!.IsEnabled&&editor.CurrentTrigger==before,"Global settings stay pending until apply");
        await ApplyGlobalSettingsAsync();
        Check(!SettingsDirty&&editor.CurrentBlockedApplications.SequenceEqual(new[]{"test.exe"}),"Global settings apply together");
        readSettingsBlacklist=()=>new[]{"unsaved.exe"};RefreshSettingsDirty();
        var root=ConfigurationDialogRoot;
        var closing=ConfirmSettingsCloseAsync();await Task.Delay(180);
        var dialog=VisualTreeHelper.GetOpenPopupsForXamlRoot(root).SelectMany(p=>SettingsElements(p.Child)).OfType<ContentDialog>().FirstOrDefault();
        Check(dialog is not null,"Closing dirty settings prompts");
        if(dialog is not null)
        {
            var close=SettingsElements(dialog).OfType<Button>().Single(b=>b.Name=="CloseButton");
            ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(close).GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();
            await closing;
        }
        Check(SettingsDirty&&settingsWindow is not null,"Continue editing retains global settings draft");
        await Task.Delay(300);await SmokeScenario.RenderAsync((FrameworkElement)settingsWindow!.Content,Path.Combine(directory,"global-settings.png"));
        settingsCloseApproved=true;settingsWindow.Close();await Task.Delay(80);
        Check(editor.CurrentBlockedApplications.SequenceEqual(new[]{"test.exe"}),"Cancel does not save pending global settings");
        await editor.SaveGlobalSettingsAsync(before,blocked);
    }
}
#endif
