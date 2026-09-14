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
    internal async Task CheckMacrosAsync(List<string> results,string directory)
    {
        void Check(bool ok,string text)=>results.Add((ok?"PASS: ":"FAIL: ")+text);
        selectedButtonId="right";RefreshButton();
        var macro=new MacroDefinition("复制再粘贴",[new(new ushort[]{17,67},0),new(new ushort[]{17,86},10)]);
        draft.SetMacro(profileId,Entry!.Id,macro);
        var preset=draft.SavePreset(profileId,Entry.Id,macro.Name,null);
        var copy=ConfigurationCodec.Deserialize(ConfigurationCodec.Serialize(draft.Snapshot()));
        var menu=MenuDocumentCodec.Deserialize(MenuDocumentCodec.Serialize(draft.ExportMenu(profileId)));
        Check(copy.Profiles.Single(p=>p.Id==profileId).Entries.Single(e=>e.Id==Entry.Id).Macro?.Steps.Count==2
            &&copy.Presets!.Single(p=>p.Id==preset.Id).Macro?.Name==macro.Name
            &&menu.Menu.Entries.Single(e=>e.Id==Entry.Id).Macro?.Steps[1].Keys.SequenceEqual(new ushort[]{17,86})==true,
            "Macro survives configuration, preset and menu export");
        var platform=new MacroCheckPlatform();var registry=new ActionRegistry();registry.Register(new WindowsPlugin(),platform);
        var context=new MouseNavigator.Contracts.ApplicationContext(123,"test.exe","test",Macro:macro);
        var executed=await registry.ExecuteAsync("windows.macro",context);
        Check(executed.Succeeded&&platform.Calls.Count==2&&platform.Calls[0].SequenceEqual(new ushort[]{17,67})&&platform.Calls[1].SequenceEqual(new ushort[]{17,86}),"Macro dispatches shortcuts in order");
        platform.Calls.Clear();platform.Fail=true;
        Check(!(await registry.ExecuteAsync("windows.macro",context)).Succeeded&&platform.Calls.Count==1,"Failure stops subsequent steps");
        platform.Calls.Clear();platform.Fail=false;platform.Active=false;
        Check(!(await registry.ExecuteAsync("windows.macro",context)).Succeeded&&platform.Calls.Count==0,"Lost focus stops before injecting keys");
        platform.Active=true;using var cancel=new CancellationTokenSource(20);
        var waiting=context with{Macro=new("等待",[new(new ushort[]{17,67},500)])};
        Check(!(await registry.ExecuteAsync("windows.macro",waiting,cancel.Token)).Succeeded&&platform.Calls.Count==0,"Cancellation interrupts delay");
        var original=ConfigurationCodec.Serialize(draft.Snapshot());
        DialogOpenedForSmoke=async d=>{
            ((TextBox)((StackPanel)d.Content).Children[0]).Text="不保存";
            await Task.Delay(150);await SmokeScenario.RenderAsync(d,Path.Combine(directory,"macro-dialog.png"));
            var close=Descendants(d).OfType<Button>().First(b=>b.Name=="CloseButton");
            ((IInvokeProvider)new ButtonAutomationPeer(close).GetPattern(PatternInterface.Invoke)).Invoke();
        };
        try{await EditMacroAsync();}finally{DialogOpenedForSmoke=null;}
        Check(ConfigurationCodec.Serialize(draft.Snapshot())==original,"Cancelling macro editor preserves configuration");
    }
    private sealed class MacroCheckPlatform:IPlatformActions
    {
        public bool Active=true,Fail;
        public List<ushort[]> Calls=[];
        public bool IsMacroTargetActive(nint source)=>Active;
        public ActionResult SendShortcut(nint source,params ushort[] keys){Calls.Add(keys);return Fail?ActionResult.Failure("test"):ActionResult.Success("test");}
    }
}
#endif