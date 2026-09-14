#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using MouseNavigator.Actions.BuiltIn;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    internal async Task CheckActionEditorAsync(List<string> results,string directory)
    {
        void Check(bool ok,string name)=>results.Add((ok?"PASS: ":"FAIL: ")+name);
        async Task Click(ContentDialog dialog,string name)
        {
            await Task.Delay(120);
            var button=Descendants(dialog).OfType<Button>().First(b=>b.Name==name);
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
        }
        selectedButtonId="right";RefreshButton();await Task.Delay(150);
        presetSearch.Text="播放器";await Task.Delay(100);
        Check(presetCards.Children.Count==7,"Search finds media presets by category");
        presetSearch.Text="no-such-preset";await Task.Delay(100);
        Check(presetCards.Children.Count==0,"Search excludes nonmatching presets");
        presetSearch.Text="";await Task.Delay(100);
        var buttons=actionChoices.Children.OfType<Button>().ToArray();
        Check(buttons.All(b=>Math.Abs(b.ActualWidth-buttons[0].ActualWidth)<1),"All action buttons have the same actual width");
        var original=ConfigurationCodec.Serialize(draft.Snapshot());var height=properties.ActualHeight;
        try
        {
            DialogOpenedForSmoke=async d=>{
                await Task.Delay(100);
                Check(ReferenceEquals(d.Content,shortcutPanel)&&!properties.Children.Contains(shortcutPanel),"Shortcut configuration opens separately");
                await SmokeScenario.RenderAsync(d,Path.Combine(directory,"shortcut-dialog.png"));
                await Click(d,"CloseButton");
            };
            await ChooseActionAsync("$shortcut");
            Check(original==ConfigurationCodec.Serialize(draft.Snapshot()),"Cancelling shortcut dialog preserves original action");
            DialogOpenedForSmoke=async d=>{
                shortcutName.Text="保存";mainKey.SelectedItem=mainKey.Items.Cast<ComboBoxItem>().Single(i=>(ushort)i.Tag==0x53);
                await Click(d,"PrimaryButton");
            };
            await ChooseActionAsync("$shortcut");
            Check(draft.Shortcut(Entry!.ActionId)?.Keys.SequenceEqual(new ushort[]{17,83})==true,"Confirming shortcut dialog saves Ctrl+S");
            var before=ConfigurationCodec.Serialize(draft.Snapshot());
            DialogOpenedForSmoke=async d=>{
                draft.SetLaunch(profileId,Entry!.Id,new(@"C:\Example\App.exe","--test"));RefreshLaunchFields();
                await SmokeScenario.RenderAsync(d,Path.Combine(directory,"application-dialog.png"));
                await Click(d,"CloseButton");
            };
            await ChooseActionAsync("windows.applications.launch");
            Check(before==ConfigurationCodec.Serialize(draft.Snapshot()),"Cancelling application dialog restores shortcut");
            DialogOpenedForSmoke=async d=>{
                draft.SetLaunch(profileId,Entry!.Id,new(@"C:\Example\App.exe","--test"));RefreshLaunchFields();
                launchArguments.Text="--open \"a b.txt\"";
                await Click(d,"PrimaryButton");
            };
            await ChooseActionAsync("windows.applications.launch");
            Check(Entry?.Launch==new ApplicationLaunch(@"C:\Example\App.exe","--open \"a b.txt\""),"Confirming application dialog saves path and edited arguments");
            await Task.Delay(100);
            Check(Math.Abs(height-properties.ActualHeight)<1,"Action configuration leaves sidebar height unchanged");
            foreach(var template in DefaultProfiles.Templates())
            {
                var id=draft.ImportMenu(new(1,template,[]));
                Check(!draft.Find(id).IsDefault&&draft.Find(id).Entries.All(e=>actions.Any(a=>a.Id==e.ActionId)),"Template imports with valid actions: "+template.Name);
            }
        }
        finally{DialogOpenedForSmoke=null;}
    }
    internal async Task CheckActionsAsync(List<string> results,string directory)
    {
        void Check(bool ok,string name)=>results.Add((ok?"PASS: ":"FAIL: ")+name);
        await Task.Delay(200);preview.SelectAt(100,0);
        var categories=actionChoices.Children.OfType<Button>().Where(b=>b.Flyout is MenuFlyout).ToArray();
        Check(categories.Length==6,"Six action categories render");
        DialogOpenedForSmoke=async d=>{
            draft.SetLaunch(profileId,Entry!.Id,new(@"C:\Example\App.exe"));RefreshLaunchFields();
            await Task.Delay(100);
            var confirm=Descendants(d).OfType<Button>().First(b=>b.Name=="PrimaryButton");
            ((IInvokeProvider)new ButtonAutomationPeer(confirm).GetPattern(PatternInterface.Invoke)).Invoke();
        };
        foreach(var button in categories)
        {
            var flyout=(MenuFlyout)button.Flyout;flyout.ShowAt(button);await Task.Delay(70);
            var item=(MenuFlyoutItem)flyout.Items[0];
            var peer=new MenuFlyoutItemAutomationPeer(item);
            ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
            await Task.Delay(350);
            Check(Entry?.ActionId==(string)item.Tag,"Category selection: "+button.Content);
        }
        DialogOpenedForSmoke=null;
        var launch=new ApplicationLaunch(@"C:\Program Files\Example\Player.exe","--open \"a b.mp4\"");
        draft.SetLaunch(profileId,Entry!.Id,launch);RefreshButton();
        launchArguments.Text="--test \"space in argument\"";
        await Task.Delay(100);launch=launch with{Arguments=launchArguments.Text};
        var preset=draft.SavePreset(profileId,Entry!.Id,"应用启动",null);
        var restored=ConfigurationCodec.Deserialize(ConfigurationCodec.Serialize(draft.Snapshot()));
        var menu=MenuDocumentCodec.Deserialize(MenuDocumentCodec.Serialize(draft.ExportMenu(profileId)));
        Check(restored.Profiles.Single(p=>p.Id==profileId).Entries.Single(e=>e.Id==Entry!.Id).Launch==launch
            &&restored.Presets!.Single(p=>p.Id==preset.Id).Launch==launch
            &&menu.Menu.Entries.Single(e=>e.Id==Entry!.Id).Launch==launch,"Application path and arguments survive backup, preset and menu export");
        var platform=new ActionCheckPlatform();var registry=new ActionRegistry();registry.Register(new WindowsPlugin(),platform);
        var context=new MouseNavigator.Contracts.ApplicationContext(123,"test.exe","test",launch);
        var browser=await registry.ExecuteAsync("windows.browser.newTab",context);
        Check(browser.Succeeded&&platform.Keys!.SequenceEqual(new ushort[]{0x11,0x54})&&platform.Source==123,"Browser action targets original window with Ctrl+T");
        await registry.ExecuteAsync("windows.media.playPause",context);
        Check(platform.Keys!.SequenceEqual(new ushort[]{0xB3}),"Player action dispatches media key");
        var started=await registry.ExecuteAsync("windows.applications.launch",context);
        Check(started.Succeeded&&platform.Launch==launch,"Launch action passes executable and arguments");
        Check(!(await registry.ExecuteAsync("windows.applications.launch",context with{Launch=null})).Succeeded,"Unconfigured application returns a readable failure");
        Check(actionChoices.Children.OfType<Button>().Any(b=>b.IsEnabled&&b.Content.ToString()=="宏操作"),"Macro editor entry is available");
        await SmokeScenario.RenderAsync((FrameworkElement)XamlRoot.Content,Path.Combine(directory,"actions.png"));
    }
    private sealed class ActionCheckPlatform:IPlatformActions
    {
        public ushort[]? Keys;public nint Source;public ApplicationLaunch? Launch;
        public ActionResult SendShortcut(nint source,params ushort[] keys){Source=source;Keys=keys;return ActionResult.Success("recorded");}
        public ActionResult LaunchApplication(ApplicationLaunch target){Launch=target;return ActionResult.Success("recorded");}
    }
}
#endif