#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    internal async Task CheckFolderPreviewAsync(MainWindow main,List<string> results,string directory)
    {
        void Check(bool ok,string text)=>results.Add((ok?"PASS: ":"FAIL: ")+text);
        void Invoke(Button button)=>((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
        var parent=draft.AddPresetFolder("父文件夹");var child=draft.AddPresetFolder("子文件夹",parent);
        draft.SavePreset(profileId,"right","嵌套预设","\uE734",child);
        currentPresetFolder=null;RefreshPresets();
        for(var i=0;i<5;i++)
        {
            Invoke(presetCards.Children.OfType<Button>().Single(b=>(string)b.Tag==parent));await Task.Delay(60);
            Invoke(presetCards.Children.OfType<Button>().Single(b=>(string)b.Tag==child));await Task.Delay(60);
            Check(currentPresetFolder==child&&presetCards.Children.Count==1,"Enter nested folder without rebuilding active picker "+i);
            Invoke(presetBack);await Task.Delay(60);Check(currentPresetFolder==parent,"Return to parent "+i);
            Invoke(presetBack);await Task.Delay(60);
        }
        var copy=ConfigurationCodec.Deserialize(ConfigurationCodec.Serialize(draft.Snapshot()));
        Check(copy.PresetFolders!.Single(f=>f.Id==child).ParentId==parent,"Nested folder persists");
        var invalid=copy with{PresetFolders=[new(parent,"a",child),new(child,"b",parent)]};
        try{ConfigurationCodec.Validate(invalid);Check(false,"Reject cyclic folders");}catch(ArgumentException){Check(true,"Reject cyclic folders");}
        DialogOpenedForSmoke=async d=>{
            var body=(StackPanel)((ScrollViewer)d.Content).Content;
            ((CheckBox)body.Children[1]).IsChecked=false;
            ((ColorPicker)body.Children[2]).Color=global::Windows.UI.Color.FromArgb(255,50,80,110);
            ((Slider)body.Children[3]).Value=30;
            ((Slider)body.Children[5]).Value=20;
            await Task.Delay(80);
            Invoke(Descendants(d).OfType<Button>().First(b=>b.Name=="PrimaryButton"));
        };
        try{await ChoosePreviewAppearanceAsync();}finally{DialogOpenedForSmoke=null;}
        var appearance=draft.Find(profileId).PreviewAppearance!;
        var menu=MenuDocumentCodec.Deserialize(MenuDocumentCodec.Serialize(draft.ExportMenu(profileId)));
        Check(menu.Menu.PreviewAppearance==appearance&&appearance.BackgroundColor=="#32506E"&&Math.Abs(appearance.BackgroundOpacity-.7)<.001&&appearance.CornerRadius==20,"Preview appearance persists in menu");
        var panel=new WindowPreviewWindow();
        try
        {
            panel.ShowAt(main.AppWindow.Position.X+main.AppWindow.Size.Width/2,main.AppWindow.Position.Y+main.AppWindow.Size.Height/2,
                [new(new(0,0),"预览外观示例")],appearance);
            await Task.Delay(150);var canvas=(Canvas)panel.Content;
            var color=((SolidColorBrush)canvas.Background).Color;
            Check(color.R==50&&color.G==80&&color.B==110&&canvas.Children.OfType<Border>().First().CornerRadius.TopLeft==20,"Actual preview applies color and corner radius");
            await SmokeScenario.RenderAsync(canvas,Path.Combine(directory,"preview.png"));
        }
        finally{panel.HidePreview();panel.Close();}
        await SmokeScenario.RenderAsync((FrameworkElement)XamlRoot.Content,Path.Combine(directory,"editor.png"));
    }
}
#endif