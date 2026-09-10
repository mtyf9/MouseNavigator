#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    internal async Task CheckManagementAsync(List<string> results,string directory)
    {
        void Check(bool value,string text)=>results.Add((value?"PASS: ":"FAIL: ")+text);
        async Task Click(Button button)
        {
            var peer=new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(button);
            ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)peer.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();await Task.Delay(100);
        }
        async Task DialogButton(ContentDialog dialog,string name)
        {
            await Task.Delay(100);await Click(Descendants(dialog).OfType<Button>().Single(b=>b.Name==name));
        }
        await Task.Delay(250);
        profiles.SelectedItem=profiles.Items.Cast<ComboBoxItem>().Single(i=>(string)i.Tag=="explorer");await Task.Delay(100);
        DialogOpenedForSmoke=async dialog=>{((TextBox)dialog.Content).Text="新应用测试";await DialogButton(dialog,"PrimaryButton");};
        profiles.IsDropDownOpen=true;await Task.Delay(80);profiles.SelectedItem=profiles.Items.Cast<ComboBoxItem>().Single(i=>(string)i.Tag=="$new");
        for(var i=0;i<60&&(profileId=="explorer"||newMenuPending);i++)await Task.Delay(50);
        Check(draft.Find(profileId).Name=="新应用测试"&&!dialogOpen&&!newMenuPending,"Creating a menu from a non-default dropdown completes without an unhandled exception");
        DialogOpenedForSmoke=null;
        var created=profileId;
        PickProgramsRequested=()=>Task.FromResult<IReadOnlyList<string>>(["notepad.exe","explorer.exe","notepad.exe"]);
        await ChooseProgramsAsync();priority.Value=30;await Task.Delay(80);
        Check(draft.Find(created).Applications.Count==2&&matchWarning.Text.Contains("重复匹配"),"Program selection adds unique executable names and reports overlapping matches");
        menuEnabled.IsOn=false;await Task.Delay(80);
        Check(new ProfileResolver(draft.Snapshot().Profiles).Resolve(new(0,"notepad","Test")).IsDefault,"Disabled application menu falls back to the default");
        menuEnabled.IsOn=true;await Task.Delay(80);
        Check(new ProfileResolver(draft.Snapshot().Profiles).Resolve(new(0,"explorer","Test")).Id==created,"Highest-priority enabled match wins a duplicate process binding");
        globalDefault.IsChecked=true;await Task.Delay(80);
        Check(draft.Snapshot().Profiles.Count(p=>p.IsDefault)==1&&draft.Find(created).IsDefault,"Selecting a new default atomically replaces the old default");
        draft.SetShortcut(created,"right","保存测试",[17,83]);RefreshButton();
        var document=MenuDocumentCodec.Deserialize(MenuDocumentCodec.Serialize(draft.ExportMenu(created)));
        Check(document.Shortcuts.Count==1&&document.Menu.Entries.Single(e=>e.Id=="right").ActionId==document.Shortcuts[0].Id,"Single-menu export includes the menu and its shortcut definitions");
        ImportMenuRequested=()=>Task.FromResult<MenuDocument?>(document);
        DialogOpenedForSmoke=d=>DialogButton(d,"SecondaryButton");await ImportSingleMenuAsync();
        Check(draft.Find(profileId).Name=="新应用测试+1"&&!draft.Find(profileId).IsDefault&&draft.Snapshot().Profiles.Count(p=>p.IsDefault)==1,"Same-name import saves as +1 without creating another default");
        var count=draft.Profiles.Count;
        DialogOpenedForSmoke=d=>DialogButton(d,"PrimaryButton");await ImportSingleMenuAsync();
        Check(profileId==created&&draft.Profiles.Count==count&&draft.Find(created).IsDefault,"Same-name overwrite preserves identity and the unique default");
        DialogOpenedForSmoke=null;ImportMenuRequested=null;PickProgramsRequested=null;
        var copy=new ConfigurationDraft(DefaultProfiles.Configuration());
        var geometryCorrect=true;
        foreach(var n in new[]{2,3})
        {
            copy=new(DefaultProfiles.Configuration());foreach(var e in copy.Find("global").Entries.Skip(n).ToArray())copy.RemoveButton("global",e.Id);
            copy.RotateRing("global",0,1);var p=copy.Find("global");var rotation=180d/n;var radians=(-90+rotation)*Math.PI/180;
            geometryCorrect&=Math.Abs(new MultiRingLayout(p).Rotation(0)-rotation)<0.001&&new MultiRingLayout(p).HitTest(100*Math.Cos(radians),100*Math.Sin(radians))=="top";
            copy.RotateRing("global",0,-1);geometryCorrect&=new MultiRingLayout(copy.Find("global")).Rotation(0)==0;
        }
        RotateSelectedRing(1);await Task.Delay(250);
        Check(geometryCorrect&&Math.Abs(preview.AngleForSmoke("top")-(-45))<0.01,"Half-sector rotation works for even/odd counts and updates the shared rendered positions");
        var blank=new ButtonPreset("blank","空白按钮","");var blankSession=new MenuDragSession(copy.Find("global"),null,blank);
        copy.CommitDrop("global",blankSession.PreviewAt(0,0),blank,blankSession.NewButtonId);
        Check(Descendants(presetCards.Children[0]).OfType<TextBlock>().Any(t=>t.Text=="空白按钮")&&copy.Snapshot().Profiles.Single(p=>p.IsDefault).Entries[0].ActionId=="","The first preset creates a valid blank button");
        selectedButtonId="right";RefreshButton();var presetsBefore=draft.Presets.Count;
        await Click(savePreset);
        drag=new(draft.Find(profileId),"right");moved=true;
        var savePoint=savePreset.TransformToVisual(XamlRoot.Content).TransformPoint(new(savePreset.ActualWidth/2,savePreset.ActualHeight/2));
        UpdateDropFromWindow(savePoint);await CompleteDropAsync(false);
        Check(draft.Presets.Count==presetsBefore+2&&draft.Find(profileId).Entries.Any(e=>e.Id=="right"),"Click and drag-to-save both create presets without removing the source");
        var confirmCount=0;DialogOpenedForSmoke=async d=>{confirmCount++;await DialogButton(d,"CloseButton");};
        count=draft.Find(profileId).Entries.Count;await Click(trash);await Task.Delay(250);
        Check(confirmCount==1&&draft.Find(profileId).Entries.Count==count,"Clicking the trash opens the button confirmation and cancel keeps the button");
        DialogOpenedForSmoke=d=>DialogButton(d,"PrimaryButton");
        drag=new(draft.Find(profileId),"top");moved=true;
        var trashPoint=trash.TransformToVisual(XamlRoot.Content).TransformPoint(new(trash.ActualWidth/2,trash.ActualHeight/2));
        UpdateDropFromWindow(trashPoint);await CompleteDropAsync(true);DialogOpenedForSmoke=null;
        Check(draft.Find(profileId).Entries.Count==count-1,"Dragging to the same trash target deletes after confirmation");
        var recordingCorrect=true;
        foreach(var chordKeys in new ushort[][]{[162,160,83],[91,9]})
        {
            var capture=new ShortcutRecording();foreach(var key in chordKeys)capture.Accept(key,true);
            recordingCorrect&=!capture.Completed;foreach(var key in chordKeys.Reverse())capture.Accept(key,false);
            recordingCorrect&=capture.Completed&&capture.Keys!.SequenceEqual(chordKeys[0]==91?new ushort[]{91,9}:new ushort[]{17,16,83});
        }
        var canceled=new ShortcutRecording();canceled.Accept(27,true);canceled.Accept(27,false);
        StartRecording();var installed=shortcutRecorder is not null;StopRecording();
        Check(recordingCorrect&&canceled.Completed&&canceled.Keys is null&&installed&&shortcutRecorder is null,"Shortcut recording captures ordered modifiers on release, cancels Escape, and releases its native hook");
        // Leave one rotated normal menu selected for visual inspection.
        feedback.IsOpen=false;selectedButtonId="left";RefreshButton();await Task.Delay(250);
        Descendants(XamlRoot.Content).OfType<ScrollViewer>().First(s=>s.Name=="PageScroll").ChangeView(null,0,null);
        await Task.Delay(150);
        await SmokeScenario.RenderAsync((FrameworkElement)XamlRoot.Content,Path.Combine(directory,"menu-management.png"));
        var json=ConfigurationCodec.Serialize(draft.Snapshot());Check(json==ConfigurationCodec.Serialize(ConfigurationCodec.Deserialize(json)),"Enabled/default flags, rotations and presets round-trip together");
    }
}
#endif