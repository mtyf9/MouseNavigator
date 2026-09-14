using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    private async Task ChooseMenuColorAsync()
    {
        if(dialogOpen)return;dialogOpen=true;
        var original=draft.Find(profileId);
        string?[] colors=[original.AccentColor,original.NormalColor];
        double[] opacity=[original.ActiveOpacity,original.NormalOpacity];
        var state=new ComboBox{Header="按钮状态",HorizontalAlignment=HorizontalAlignment.Stretch};
        state.Items.Add("触发（高亮）");state.Items.Add("非触发");state.SelectedIndex=0;
        var system=new CheckBox{Content="跟随系统主题色"};
        var picker=new ColorPicker{IsAlphaEnabled=false,IsMoreButtonVisible=false,IsColorSliderVisible=true,IsColorChannelTextInputVisible=true};
        var alpha=new Slider{Header="透明度（0% 不透明，100% 透明）",Minimum=0,Maximum=100,StepFrequency=1};
        bool changing=false;
        void Refresh()
        {
            changing=true;
            var index=state.SelectedIndex;
            system.IsChecked=colors[index] is null;picker.IsEnabled=colors[index] is not null;
            var c=new global::Windows.UI.ViewManagement.UISettings().GetColorValue(global::Windows.UI.ViewManagement.UIColorType.Accent);
            if(colors[index] is {} hex){var rgb=Convert.ToUInt32(hex[1..],16);c=global::Windows.UI.Color.FromArgb(255,(byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb);}
            picker.Color=c;alpha.Value=Math.Round((1-opacity[index])*100);
            changing=false;
        }
        void Preview()
        {
            if(changing)return;var index=state.SelectedIndex;
            colors[index]=system.IsChecked==true?null:$"#{picker.Color.R:X2}{picker.Color.G:X2}{picker.Color.B:X2}";
            opacity[index]=1-alpha.Value/100;picker.IsEnabled=system.IsChecked!=true;
            draft.SetAppearanceOptions(profileId,colors[0],colors[1],opacity[0],opacity[1],original.ButtonGap);RefreshPreview();
        }
        state.SelectionChanged+=(_,_)=>Refresh();system.Checked+=(_,_)=>Preview();system.Unchecked+=(_,_)=>Preview();
        picker.ColorChanged+=(_,_)=>Preview();alpha.ValueChanged+=(_,_)=>Preview();
        Refresh();
        var panel=new StackPanel{Spacing=10};panel.Children.Add(state);panel.Children.Add(system);panel.Children.Add(picker);panel.Children.Add(alpha);
        var accepted=false;
        try
        {
            var dialog=new ContentDialog{Title="悬浮窗外观",Content=new ScrollViewer{Content=panel,MaxHeight=600},PrimaryButtonText="确定",CloseButtonText="取消",XamlRoot=XamlRoot};
            accepted=await ShowDialogAsync(dialog)==ContentDialogResult.Primary;
            if(accepted)MarkDirty();
        }
        finally
        {
            if(!accepted)draft.SetAppearanceOptions(profileId,original.AccentColor,original.NormalColor,original.ActiveOpacity,original.NormalOpacity,original.ButtonGap);
            dialogOpen=false;RefreshPreview();
        }
    }
}