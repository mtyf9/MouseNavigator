#if DEBUG
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using MouseNavigator.Actions.BuiltIn;
using MouseNavigator.Windows;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    internal async Task CheckPresetStyleAsync(MainWindow main,List<string> results,string directory)
    {
        void Check(bool ok,string text)=>results.Add((ok?"PASS: ":"FAIL: ")+text);
        async Task Click(ContentDialog dialog,string name)
        {
            await Task.Delay(100);var button=Descendants(dialog).OfType<Button>().First(b=>b.Name==name);
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
        }
        selectedButtonId="right";RefreshButton();
        var original=draft.Find(profileId);
        DialogOpenedForSmoke=async d=>{
            var body=(StackPanel)((ScrollViewer)d.Content).Content;
            ((CheckBox)body.Children[1]).IsChecked=false;
            ((ColorPicker)body.Children[2]).Color=global::Windows.UI.Color.FromArgb(255,40,150,210);
            ((Slider)body.Children[3]).Value=35;
            ((ComboBox)body.Children[0]).SelectedIndex=1;
            ((CheckBox)body.Children[1]).IsChecked=false;
            ((ColorPicker)body.Children[2]).Color=global::Windows.UI.Color.FromArgb(255,120,40,80);
            ((Slider)body.Children[3]).Value=55;
            await Click(d,"PrimaryButton");
        };
        await ChooseMenuColorAsync();DialogOpenedForSmoke=null;buttonGap.Value=12;
        var edited=draft.Find(profileId);
        var restored=ConfigurationCodec.Deserialize(ConfigurationCodec.Serialize(draft.Snapshot())).Profiles.Single(p=>p.Id==profileId);
        Check(restored.AccentColor=="#2896D2"&&restored.NormalColor=="#782850"&&Math.Abs(restored.NormalOpacity-.45)<.001&&Math.Abs(restored.ActiveOpacity-.65)<.001&&restored.ButtonGap==12,"Both state colors, opacity and gap persist");
        var angle=-90+40d;
        Check(new MultiRingLayout(edited).HitTest(100*Math.Cos(angle*Math.PI/180),100*Math.Sin(angle*Math.PI/180)) is null
            &&new MultiRingLayout(edited with{ButtonGap=0}).HitTest(100*Math.Cos(angle*Math.PI/180),100*Math.Sin(angle*Math.PI/180)) is not null,"Gap affects hit testing");
        var macro=new MacroDefinition("测试宏",[new(new ushort[]{17,67},150)]);
        const string png="iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aMfoAAAAASUVORK5CYII=";
        draft.SetMacro(profileId,Entry!.Id,macro);draft.SetAppearance(profileId,Entry.Id,"宏预设","\uE74E",png);RefreshButton();
        DialogOpenedForSmoke=async d=>{
            var body=(StackPanel)d.Content;((TextBox)body.Children[2]).Text="常用宏";
            await Click(d,"PrimaryButton");
        };
        await SaveSelectedPresetAsync();DialogOpenedForSmoke=null;
        var preset=draft.Presets.Last();var folder=draft.PresetFolders.Single(f=>f.Name=="常用宏");
        var session=new MenuDragSession(draft.Find(profileId),null,preset);var proposal=session.PreviewAt(0,0);
        draft.CommitDrop(profileId,proposal,preset,session.NewButtonId);
        var added=draft.Find(profileId).Entries.Single(e=>e.Id==session.NewButtonId);
        Check(added.Macro?.Steps.Single().Keys.SequenceEqual(new ushort[]{17,67})==true&&added.Glyph=="\uE74E"&&added.Image==png&&preset.FolderId==folder.Id,"Preset drop preserves macro, image, glyph and folder");
        draft.SetMacro(profileId,added.Id,new("修改副本",[new(new ushort[]{17,86},0)]));
        Check(preset.Macro?.Name=="测试宏"&&preset.Macro.Steps.Single().Keys.Last()==67,"Editing dropped macro does not modify preset");
        var snapshot=ConfigurationCodec.Deserialize(ConfigurationCodec.Serialize(draft.Snapshot()));
        Check(snapshot.PresetFolders!.Any(f=>f.Id==folder.Id)&&snapshot.Presets!.Last().FolderId==folder.Id,"Folders survive backup round trip");
        currentPresetFolder=folder.Id;RefreshPresets();
        Check(presetCards.Children.Count==1,"Folder filters its stored preset");
        var ring=new RingWindow();var registry=new ActionRegistry();registry.Register(new WindowsPlugin(),new StylePlatform());
        var pos=main.AppWindow.Position;var size=main.AppWindow.Size;
        var x=pos.X+size.Width/2;var y=pos.Y+size.Height/2;var scale=OverlayWindow.ScaleAt(x,y);
        var px=x+(int)(85*scale);var py=y+(int)(85*scale);
        uint Pixel(){var dc=GetDC(0);try{return GetPixel(dc,px,py);}finally{ReleaseDC(0,dc);}}
        await Task.Delay(180);var before=Pixel();
        try
        {
            ring.SetMenu(original with{ButtonGap=12},registry);ring.ShowAt(x,y);await Task.Delay(200);
            Check(Pixel()==before&&before!=uint.MaxValue,"Runtime transparent gap reveals underlying window pixels");
            await SmokeScenario.RenderAsync(preview,Path.Combine(directory,"ring-style.png"));
        }
        finally{ring.HideRing();ring.Close();}
    }
    private sealed class StylePlatform:IPlatformActions{public ActionResult SendShortcut(nint source,params ushort[] keys)=>ActionResult.Success("test");}
    [DllImport("user32.dll")] private static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hwnd,nint dc);
    [DllImport("gdi32.dll")] private static extern uint GetPixel(nint dc,int x,int y);
}
#endif