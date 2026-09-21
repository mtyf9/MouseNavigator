using Microsoft.UI.Xaml;
using MouseNavigator.Contracts;
using Microsoft.UI.Xaml.Controls;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    internal async Task<bool> ConfirmPendingChangesAsync(string nextAction)
    {
        if(!IsDirty)return true;
        if(dialogOpen)return false;
        dialogOpen=true;StopRecording();CancelDrag();
        try
        {
            var result=await ShowDialogAsync(new ContentDialog{
                XamlRoot=XamlRoot,Title="保存未保存的修改？",
                Content="当前编辑有未保存的修改。保存会应用全部待保存的修改，放弃会恢复到上次保存状态。",
                PrimaryButtonText="保存并"+nextAction,SecondaryButtonText="放弃并"+nextAction,
                CloseButtonText="继续编辑",DefaultButton=ContentDialogButton.Primary});
            if(result==ContentDialogResult.None)return false;
            if(result==ContentDialogResult.Primary)await SaveDraftAsync();
            else Load(saved,false);
            return true;
        }
        catch(Exception ex){Notify("保存失败，已保留编辑："+ex.Message,InfoBarSeverity.Error);return false;}
        finally{dialogOpen=false;}
    }
    internal async Task SaveGlobalSettingsAsync(TriggerSettings trigger,IReadOnlyList<string> blocked)
    {
        var next=saved with{Trigger=trigger,BlockedApplications=blocked.ToArray()};
        MouseNavigator.Core.ConfigurationCodec.Validate(next);
        if(SaveRequested is null)throw new InvalidOperationException("配置保存服务不可用。");
        await SaveRequested(next);saved=next;draft.SetTrigger(trigger);draft.SetBlockedApplications(blocked);
        Notify("全局设置已保存并生效。",InfoBarSeverity.Success);
    }
    internal IReadOnlyList<string> CurrentBlockedApplications=>saved.BlockedApplications??[];
    internal TriggerSettings CurrentTrigger=>saved.Trigger??new();
    internal async Task ResetMenusAsync()
    {
        var reset=DefaultProfiles.Configuration() with{Presets=draft.Presets.ToArray(),PresetFolders=draft.PresetFolders.ToArray(),PresetOrder=draft.PresetOrder.ToArray(),Trigger=CurrentTrigger,BackgroundPresets=draft.BackgroundPresets.ToArray(),BlockedApplications=CurrentBlockedApplications,AppearancePresets=draft.AppearancePresets.ToArray()};
        if(SaveRequested is null)throw new InvalidOperationException("配置保存服务不可用。");
        await SaveRequested(reset);saved=reset;Load(reset,false);
    }
}