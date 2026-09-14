#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using MouseNavigator.Core;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    internal async Task CheckAppearanceAsync(List<string> results,string directory)
    {
        void Check(bool ok,string text)=>results.Add((ok?"PASS: ":"FAIL: ")+text);
        async Task Close(ContentDialog dialog,string name)
        {
            await Task.Delay(100);
            var button=Descendants(dialog).OfType<Button>().First(b=>b.Name==name);
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
        }
        await Task.Delay(180);
        var original=draft.Find(profileId).AccentColor;
        DialogOpenedForSmoke=async d=>{
            var panel=(StackPanel)((ScrollViewer)d.Content).Content;
            ((CheckBox)panel.Children[1]).IsChecked=false;
            ((ColorPicker)panel.Children[2]).Color=global::Windows.UI.Color.FromArgb(255,200,60,90);
            await Close(d,"CloseButton");
        };
        await ChooseMenuColorAsync();
        Check(draft.Find(profileId).AccentColor==original,"Cancel color picker restores menu color");
        DialogOpenedForSmoke=async d=>{
            var panel=(StackPanel)((ScrollViewer)d.Content).Content;((CheckBox)panel.Children[1]).IsChecked=false;
            ((ColorPicker)panel.Children[2]).Color=global::Windows.UI.Color.FromArgb(255,40,150,210);
            await Close(d,"PrimaryButton");
        };
        await ChooseMenuColorAsync();DialogOpenedForSmoke=null;
        var copy=ConfigurationCodec.Deserialize(ConfigurationCodec.Serialize(draft.Snapshot()));
        Check(copy.Profiles.Single(p=>p.Id==profileId).AccentColor=="#2896D2","Menu color survives configuration round trip");
        foreach(var count in new[]{1,6,20,50})
        {
            var layout=new WindowPreviewLayout(1400,820,count);
            var rects=Enumerable.Range(0,layout.Capacity).Select(layout.Card).ToArray();
            Check(rects.All(r=>r.Width>=150&&r.Height>=100&&r.Y+r.Height<=layout.Height-64)&&layout.Capacity>=Math.Min(count,40),
                "Adaptive preview capacity and bounds for "+count+" windows");
        }
        await Task.Delay(150);
        await SmokeScenario.RenderAsync((FrameworkElement)XamlRoot.Content,Path.Combine(directory,"editor.png"));
    }
}
#endif