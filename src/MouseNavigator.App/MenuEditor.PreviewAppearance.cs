using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MouseNavigator.Contracts;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    private readonly Button previewAppearanceButton=new(){Content=ButtonIcons.Create("\uE713",null,14),Width=28,Height=28,Padding=new Thickness(4),Visibility=Visibility.Collapsed};
#if DEBUG
    internal Func<WindowPreviewWindow,StackPanel,Task>? PreviewOpenedForSmoke;
#endif
    private Task ChoosePreviewAppearanceAsync()=>OpenPreviewAppearanceAsync(false);
    private Task PreviewWindowAppearanceAsync()=>OpenPreviewAppearanceAsync(true);
    private async Task OpenPreviewAppearanceAsync(bool previewOnly)
    {
        if(dialogOpen)return;dialogOpen=true;
        var targetId=profileId;
        var original=draft.Find(targetId).PreviewAppearance??new();
        var editorWindow=new WindowPreviewWindow(editing:true);
        string?[] colors=[original.BackgroundColor,original.CardColor,original.HighlightColor,original.TextColor];
        var follow=new CheckBox{Content="跟随悬浮窗背景样式和配色",IsChecked=original.FollowMenuBackground};
        var background=new Slider{Header="背景透明度（%）",Minimum=0,Maximum=100,Value=(1-original.BackgroundOpacity)*100,StepFrequency=1};
        var cards=new Slider{Header="卡片透明度（%）",Minimum=0,Maximum=100,Value=(1-original.CardOpacity)*100,StepFrequency=1};
        var radius=new Slider{Header="卡片圆角",Minimum=0,Maximum=30,Value=original.CornerRadius,StepFrequency=1};
        var body=new StackPanel{Spacing=10};body.Children.Add(follow);
        var colorButtons=new List<Button>();
        WindowPreviewAppearance Value()=>new(colors[0],colors[1],colors[2],colors[3],1-background.Value/100,1-cards.Value/100,radius.Value,original.TransitionMilliseconds,follow.IsChecked==true);
        void Preview()
        {
            var following=follow.IsChecked==true;
            foreach(var button in colorButtons)button.IsEnabled=!following;
            background.IsEnabled=cards.IsEnabled=!following;
            if(following)editorWindow.ShowEditorColorPanel(null);
            editorWindow.ApplyEditorAppearance(PreviewStyle.Resolve(draft.Find(targetId),Value()));
        }
        string[] titles=["面板背景","普通卡片","高亮卡片","文字"];
        for(var i=0;i<titles.Length;i++)
        {
            // Each row owns a stable index; no transient selector index is used.
            var index=i;var a=new WindowPreviewAppearance();
            var fallback=index switch{0=>PreviewPalette.Background(a).Color,1=>PreviewPalette.Card(a).Color,2=>PreviewPalette.Highlight(a).Color,_=>PreviewPalette.Text(a).Color};
            var defaults=new RadioButton{Content="跟随系统主题色",GroupName="preview-color-"+index,IsChecked=colors[index] is null};
            var picker=new ColorPicker{IsAlphaEnabled=false,IsMoreButtonVisible=false,Color=PreviewPalette.Color(colors[index],fallback),IsEnabled=colors[index] is not null};
            var button=ColorControl("配置颜色  ›",picker,defaults);button.Tag="preview-color-"+index;
            button.HorizontalAlignment=HorizontalAlignment.Right;
            var flyout=(Flyout)button.Flyout;var panel=(FrameworkElement)flyout.Content;flyout.Content=null;button.Flyout=null;
            var colorBody=new StackPanel{Width=280,Spacing=12,Children={new TextBlock{Text=titles[index],FontSize=18,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold},panel}};
            var colorView=new Viewbox{Child=colorBody,Stretch=Microsoft.UI.Xaml.Media.Stretch.Uniform,StretchDirection=Microsoft.UI.Xaml.Controls.StretchDirection.DownOnly,VerticalAlignment=VerticalAlignment.Top};
            button.Click+=(_,_)=>editorWindow.ShowEditorColorPanel(colorView);
            void ChangeColor()
            {
                picker.IsEnabled=defaults.IsChecked!=true;
                colors[index]=defaults.IsChecked==true?null:$"#{picker.Color.R:X2}{picker.Color.G:X2}{picker.Color.B:X2}";
                if(defaults.IsChecked==true)picker.Color=fallback;
                Preview();
            }
            defaults.Checked+=(_,_)=>ChangeColor();defaults.Unchecked+=(_,_)=>ChangeColor();picker.ColorChanged+=(_,_)=>ChangeColor();
            var row=new Grid{ColumnSpacing=8};row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
            row.Children.Add(new TextBlock{Text=titles[index],VerticalAlignment=VerticalAlignment.Center});Grid.SetColumn(button,1);row.Children.Add(button);
            colorButtons.Add(button);body.Children.Add(row);
        }
        follow.Checked+=(_,_)=>Preview();follow.Unchecked+=(_,_)=>Preview();
        background.ValueChanged+=(_,_)=>Preview();cards.ValueChanged+=(_,_)=>Preview();radius.ValueChanged+=(_,_)=>Preview();
        body.Children.Add(background);body.Children.Add(cards);body.Children.Add(radius);Preview();
        try
        {

            var owner=Microsoft.UI.Windowing.AppWindow.GetFromWindowId(XamlRoot.ContentIslandEnvironment.AppWindowId);
            var x=0;var y=0;
            if(owner is not null){x=owner.Position.X+owner.Size.Width/2;y=owner.Position.Y+owner.Size.Height/2;}
            var editing=editorWindow.EditAppearanceAsync(body,PreviewStyle.Resolve(draft.Find(targetId),Value()),x,y,previewOnly);
#if DEBUG
            if(PreviewOpenedForSmoke is not null)await PreviewOpenedForSmoke(editorWindow,body);
#endif
            if(!await editing)return;
            draft.SetPreviewAppearance(targetId,Value());MarkDirty();
        }
        finally{editorWindow.CloseAppearanceEditor();dialogOpen=false;}
    }
}