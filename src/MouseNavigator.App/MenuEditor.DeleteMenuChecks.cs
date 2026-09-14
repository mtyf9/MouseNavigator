#if DEBUG
using Microsoft.UI.Xaml.Controls;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    internal async Task CheckDeleteMenuAsync(List<string> results)
    {
        async Task Run(bool current,bool confirm)
        {
            var id=draft.Add();var p=draft.Find(id);draft.Update(id,"菜单1",p.Applications,p.Priority);
            profileId=current?id:draft.Profiles.Single(x=>x.IsDefault).Id;selectedButtonId=null;RefreshProfiles();
            await Task.Delay(150);
            DialogOpenedForSmoke=async dialog=>{
                await Task.Delay(150);
                var b=Descendants(dialog).OfType<Button>().Single(b=>b.Name==(confirm?"PrimaryButton":"CloseButton"));
                var peer=new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(b);
                ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)peer.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();
            };
            profiles.IsDropDownOpen=true;await Task.Delay(150);
            var item=profiles.Items.Cast<ComboBoxItem>().Single(i=>(string)i.Tag==id);
            ((ProfileChoice)item.Content).Delete.Execute(null);
            for(var i=0;i<80&&(newMenuPending||dialogOpen);i++)await Task.Delay(50);
            await Task.Delay(200);
            var correct=draft.Profiles.Any(x=>x.Id==id)!=confirm&&!dialogOpen&&!newMenuPending;
            results.Add((correct?"PASS: ":"FAIL: ")+$"Delete menu current={current} confirm={confirm}");
            profiles.SelectedItem=profiles.Items.Cast<ComboBoxItem>().First(i=>(string)i.Tag!="$new"&&(string)i.Tag!=profileId);
            await Task.Delay(150);
            results.Add(draft.Profiles.Any(x=>x.Id==profileId)?"PASS: switch after delete/cancel":"FAIL: invalid active menu");
        }
        try{await Run(true,false);await Run(true,true);await Run(false,true);}
        finally{DialogOpenedForSmoke=null;}
    }
}
#endif