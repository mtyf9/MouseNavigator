#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    internal async Task CheckUnsavedAsync(List<string> results)
    {
        async Task Answer(ContentDialog dialog,string name)
        {
            await Task.Delay(100);
            var button=Descendants(dialog).OfType<Button>().Single(b=>b.Name==name);
            var peer=new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(button);
            ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)peer.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();
        }
        var originalSave=SaveRequested;
        var source=profileId;var target=draft.Profiles.First(p=>p.Id!=source).Id;
        saved=draft.Snapshot();IsDirty=false;
        var p=draft.Find(source);draft.Update(source,"待保存",p.Applications,p.Priority);MarkDirty();
        DialogOpenedForSmoke=d=>Answer(d,"CloseButton");
        QueueProfileSelection(target);await WaitSwitch();
        results.Add(profileId==source&&IsDirty?"PASS: cancel switching preserves edits":"FAIL: cancel switch");
        DialogOpenedForSmoke=d=>Answer(d,"SecondaryButton");
        QueueProfileSelection(target);await WaitSwitch();
        results.Add(profileId==target&&!IsDirty&&draft.Find(source).Name==p.Name?"PASS: discard restores saved menu then switches":"FAIL: discard switch");
        var savedCount=0;SaveRequested=c=>{savedCount++;return Task.CompletedTask;};
        var q=draft.Find(target);draft.Update(target,"保存测试",q.Applications,q.Priority);MarkDirty();
        DialogOpenedForSmoke=d=>Answer(d,"PrimaryButton");
        QueueProfileSelection(source);await WaitSwitch();
        results.Add(profileId==source&&!IsDirty&&savedCount==1?"PASS: save before switch":"FAIL: save switch");
        MarkDirty();SaveRequested=_=>throw new IOException("模拟保存失败");
        QueueProfileSelection(target);await WaitSwitch();
        results.Add(profileId==source&&IsDirty?"PASS: save failure blocks switching":"FAIL: save failure");
        DialogOpenedForSmoke=d=>Answer(d,"CloseButton");
        results.Add(!await ConfirmPendingChangesAsync("关闭")&&IsDirty?"PASS: cancel closing keeps edits":"FAIL: close guard");
        selectedButtonId=draft.Find(source).Entries.FirstOrDefault()?.Id;RefreshButton();
        results.Add(previewAppearanceButton.Visibility==Visibility.Visible&&previewAppearanceButton.ActualWidth>0?"PASS: preview appearance gear is visible":"FAIL: preview gear");
        SaveRequested=originalSave;DialogOpenedForSmoke=null;
        async Task WaitSwitch(){for(var i=0;i<100&&profileSwitchPending;i++)await Task.Delay(50);await Task.Delay(100);}
    }
}
#endif