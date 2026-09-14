using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MouseNavigator.Contracts;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    private readonly Button previewAppearanceButton=new(){Content=ButtonIcons.Create("\uE713",null,14),Width=28,Height=28,Padding=new Thickness(4),Visibility=Visibility.Collapsed};
    private async Task ChoosePreviewAppearanceAsync()
    {
        if(dialogOpen)return;dialogOpen=true;
        var targetId=profileId;
        var original=draft.Find(targetId).PreviewAppearance??new();
        var editorWindow=new WindowPreviewWindow(editing:true);
        string?[] colors=[original.BackgroundColor,original.CardColor,original.HighlightColor,original.TextColor];
        var part=new ComboBox{Header="设置部位",HorizontalAlignment=HorizontalAlignment.Stretch};
        foreach(var title in new[]{"面板背景","普通卡片","高亮卡片","文字"})part.Items.Add(title);
        part.SelectedIndex=0;
        var defaults=new CheckBox{Content="跟随系统主题色"};
        var picker=new ColorPicker{IsAlphaEnabled=false,IsMoreButtonVisible=false};
        var background=new Slider{Header="背景透明度（%）",Minimum=0,Maximum=100,Value=(1-original.BackgroundOpacity)*100,StepFrequency=1};
        var cards=new Slider{Header="卡片透明度（%）",Minimum=0,Maximum=100,Value=(1-original.CardOpacity)*100,StepFrequency=1};
        var radius=new Slider{Header="卡片圆角",Minimum=0,Maximum=30,Value=original.CornerRadius,StepFrequency=1};
        bool changing=false;
        WindowPreviewAppearance Value()=>new(colors[0],colors[1],colors[2],colors[3],1-background.Value/100,1-cards.Value/100,radius.Value);
        void Preview()
        {
            if(changing)return;var a=Value();
            editorWindow.ApplyEditorAppearance(a);
        }
        void Refresh()
        {
            changing=true;var index=part.SelectedIndex;
            defaults.IsChecked=colors[index] is null;picker.IsEnabled=colors[index] is not null;
            var a=new WindowPreviewAppearance();
            var color=index switch{0=>PreviewPalette.Background(a).Color,1=>PreviewPalette.Card(a).Color,2=>PreviewPalette.Highlight(a).Color,_=>PreviewPalette.Text(a).Color};
            picker.Color=PreviewPalette.Color(colors[index],color);changing=false;Preview();
        }
        void ChangeColor()
        {
            if(changing)return;
            colors[part.SelectedIndex]=defaults.IsChecked==true?null:$"#{picker.Color.R:X2}{picker.Color.G:X2}{picker.Color.B:X2}";
            picker.IsEnabled=defaults.IsChecked!=true;Preview();
        }
        part.SelectionChanged+=(_,_)=>Refresh();defaults.Checked+=(_,_)=>ChangeColor();defaults.Unchecked+=(_,_)=>ChangeColor();
        picker.ColorChanged+=(_,_)=>ChangeColor();
        background.ValueChanged+=(_,_)=>Preview();cards.ValueChanged+=(_,_)=>Preview();radius.ValueChanged+=(_,_)=>Preview();
        var body=new StackPanel{Spacing=10};
        body.Children.Add(part);body.Children.Add(defaults);body.Children.Add(picker);
        body.Children.Add(background);body.Children.Add(cards);body.Children.Add(radius);
        Refresh();
        try
        {

            var owner=Microsoft.UI.Windowing.AppWindow.GetFromWindowId(XamlRoot.ContentIslandEnvironment.AppWindowId);
            var x=0;var y=0;
            if(owner is not null){x=owner.Position.X+owner.Size.Width/2;y=owner.Position.Y+owner.Size.Height/2;}
            if(!await editorWindow.EditAppearanceAsync(body,Value(),x,y))return;
            draft.SetPreviewAppearance(targetId,Value());MarkDirty();
        }
        finally{editorWindow.CloseAppearanceEditor();dialogOpen=false;}
    }
}