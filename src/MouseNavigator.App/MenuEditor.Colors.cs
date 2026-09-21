using MouseNavigator.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    private readonly StackPanel appearanceBody=new(){Spacing=10};
    private int appearanceGeneration,appearancePage; private Grid? previewAppearanceRow;
    private readonly StackPanel appearanceLayout=new(){Spacing=10};
    private void RefreshAppearancePage()
    {
        for(var i=0;i<appearanceBody.Children.Count;i++)appearanceBody.Children[i].Visibility=((i<3?i:4)==appearancePage)?Visibility.Visible:Visibility.Collapsed;
        appearanceLayout.Visibility=appearancePage==3?Visibility.Visible:Visibility.Collapsed;
    }
    private static void AlignColumnContent(DependencyObject root)
    {
        if(root is Control control)control.HorizontalContentAlignment=HorizontalAlignment.Left;
        if(root is TextBlock text)text.TextAlignment=TextAlignment.Left;
        for(var i=0;i<Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);i++)
            AlignColumnContent(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root,i));
    }

    private static Button ColorControl(string title,ColorPicker picker,RadioButton? automatic=null)
    {
        var swatch=new Microsoft.UI.Xaml.Shapes.Rectangle{Width=18,Height=18,RadiusX=3,RadiusY=3};
        var label=new TextBlock{Text=title,VerticalAlignment=VerticalAlignment.Center};
        var content=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8,Children={swatch,label}};
        void Update()=>swatch.Fill=new Microsoft.UI.Xaml.Media.SolidColorBrush(picker.Color);
        picker.ColorChanged+=(_,_)=>Update();Update();
        return new Button{Content=content,Tag=title,HorizontalContentAlignment=HorizontalAlignment.Left,HorizontalAlignment=HorizontalAlignment.Stretch,Flyout=new Flyout{Content=ColorPanel(picker,automatic)}};
    }
    private static FrameworkElement ColorPanel(ColorPicker picker,RadioButton? automatic=null)
    {
        if(automatic is null)return picker;
        var custom=new RadioButton{Content="自定义颜色",GroupName=automatic.GroupName,IsChecked=automatic.IsChecked!=true};
        automatic.Checked+=(_,_)=>custom.IsChecked=false;
        automatic.Unchecked+=(_,_)=>custom.IsChecked=true;
        custom.Checked+=(_,_)=>automatic.IsChecked=false;
        return new StackPanel{Spacing=8,Children={automatic,custom,picker}};
    }
    private void BuildAppearanceSidebar()
    {
        var generation=++appearanceGeneration;var targetId=profileId;previewAppearanceRow?.Children.Clear();appearanceBody.Children.Clear();
        var original=draft.Find(profileId);
        string?[] colors=[original.AccentColor,original.NormalColor];
        double[] opacity=[original.ActiveOpacity,original.NormalOpacity];
        var state=new ComboBox{Header="按钮状态",HorizontalAlignment=HorizontalAlignment.Stretch};
        state.Items.Add("触发（高亮）");state.Items.Add("非触发");state.SelectedIndex=0;
        var system=new RadioButton{Content="跟随系统主题色",GroupName="button-color-"+generation};
        var picker=new ColorPicker{IsAlphaEnabled=false,IsMoreButtonVisible=false,IsColorSliderVisible=true,IsColorChannelTextInputVisible=true};
        var alpha=new Slider{Header="按钮透明度（0% 不透明，100% 透明）",Minimum=0,Maximum=100,StepFrequency=1};
        var originalStyle=original.VisualStyle??new();
        var glassAlpha=new Slider{Header="毛玻璃透明度（0% 不透明，100% 透明）",Minimum=0,Maximum=100,StepFrequency=1,Value=(1-originalStyle.GlassOpacity)*100};var backgroundData=originalStyle.BackgroundImage;


        var backgroundAlpha=new Slider{Header="背景透明度（%）",Minimum=0,Maximum=100,StepFrequency=1,Value=(1-originalStyle.BackgroundOpacity)*100};
        var imageNotice=new TextBlock{Text=backgroundData is null?"尚未选择背景":"已嵌入背景图片",TextWrapping=TextWrapping.Wrap};
        var imageOptions=new StackPanel{Spacing=8,Children={backgroundAlpha,imageNotice}};
        var gradientStart=new ColorPicker{IsAlphaEnabled=false,IsMoreButtonVisible=false};
        var gradientEnd=new ColorPicker{IsAlphaEnabled=false,IsMoreButtonVisible=false};
        global::Windows.UI.Color Parse(string hex){var rgb=Convert.ToUInt32(hex[1..],16);return global::Windows.UI.Color.FromArgb(255,(byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb);}
        var solidSystem=new RadioButton{Content="跟随系统主题色",GroupName="solid-color-"+generation,IsChecked=originalStyle.SolidColor is null};
        var solidColor=new ColorPicker{IsAlphaEnabled=false,IsMoreButtonVisible=false,IsEnabled=originalStyle.SolidColor is not null,Color=originalStyle.SolidColor is {} solidHex?Parse(solidHex):PreviewPalette.RestingAccent};
        var solidAlpha=new Slider{Header="背景透明度（0% 不透明，100% 透明）",Minimum=0,Maximum=100,StepFrequency=1,Value=(1-originalStyle.SolidOpacity)*100};
        var solidOptions=new StackPanel{Spacing=8,Children={ColorControl("背景颜色",solidColor,solidSystem),solidAlpha}};
        gradientStart.Color=Parse(originalStyle.GradientStart);gradientEnd.Color=Parse(originalStyle.GradientEnd);
        var angle=new Slider{Header="色环旋转角度（度）",Minimum=0,Maximum=360,StepFrequency=5,Value=originalStyle.GradientAngle};
        var gradientPanel=new StackPanel{Spacing=8,Children={ColorControl("渐变起点颜色",gradientStart),ColorControl("渐变终点颜色",gradientEnd),angle}};
        var skin=new ComboBox{Header="外观样式",HorizontalAlignment=HorizontalAlignment.Stretch};
        foreach(var item in MenuSkins.Available)skin.Items.Add(item.Name);
        var skinIndex=MenuSkins.Available.ToList().FindIndex(s=>s.Id==originalStyle.SkinId);
        if(skinIndex<0){skin.Items.Add("未提供的背景（暂用纯色）");skin.SelectedIndex=skin.Items.Count-1;}else skin.SelectedIndex=skinIndex;
        var outlineEnabled=new CheckBox{Content="显示按钮描边",IsChecked=originalStyle.OutlineEnabled};
        var outlineAuto=new RadioButton{Content="自动选择描边颜色",GroupName="outline-color-"+generation,IsChecked=originalStyle.OutlineColor is null};
        var outlinePicker=new ColorPicker{IsAlphaEnabled=false,IsMoreButtonVisible=false,Color=Parse(originalStyle.OutlineColor??"#D2DCEB")};
        var outlineOptions=new StackPanel{Spacing=8,Children={outlineEnabled,ColorControl("描边颜色",outlinePicker,outlineAuto)}};
        var motion=new ComboBox{Header="出现动画",HorizontalAlignment=HorizontalAlignment.Stretch};
        foreach(var name in new[]{"无动画","淡入","缩放展开","向上浮现","旋转展开"})motion.Items.Add(name);
        motion.SelectedIndex=(int)originalStyle.Entrance;
        var duration=new Slider{Header="出现动画时长（毫秒）",Minimum=0,Maximum=1000,StepFrequency=10,Value=originalStyle.EntranceMilliseconds};
        var highlight=new Slider{Header="选中变色时长（毫秒，0 为立即切换）",Minimum=0,Maximum=500,StepFrequency=10,Value=originalStyle.HighlightMilliseconds};
        MenuVisualStyle StyleValue()=>new(skin.SelectedIndex<MenuSkins.Available.Count?MenuSkins.Available[skin.SelectedIndex].Id:originalStyle.SkinId,
            (MenuEntrance)motion.SelectedIndex,(int)duration.Value,(int)highlight.Value,$"#{gradientStart.Color.R:X2}{gradientStart.Color.G:X2}{gradientStart.Color.B:X2}",$"#{gradientEnd.Color.R:X2}{gradientEnd.Color.G:X2}{gradientEnd.Color.B:X2}",angle.Value,backgroundData,1-backgroundAlpha.Value/100,1-glassAlpha.Value/100,outlineEnabled.IsChecked==true,outlineAuto.IsChecked==true?null:$"#{outlinePicker.Color.R:X2}{outlinePicker.Color.G:X2}{outlinePicker.Color.B:X2}",solidSystem.IsChecked==true?null:$"#{solidColor.Color.R:X2}{solidColor.Color.G:X2}{solidColor.Color.B:X2}",1-solidAlpha.Value/100);
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
            if(changing||generation!=appearanceGeneration||targetId!=profileId)return;solidOptions.Visibility=StyleValue().SkinId=="builtin.solid"?Visibility.Visible:Visibility.Collapsed;outlineAuto.IsEnabled=outlineEnabled.IsChecked==true;outlinePicker.IsEnabled=outlineEnabled.IsChecked==true&&outlineAuto.IsChecked!=true;glassAlpha.Visibility=StyleValue().SkinId=="builtin.glass"?Visibility.Visible:Visibility.Collapsed;var index=state.SelectedIndex;
            colors[index]=system.IsChecked==true?null:$"#{picker.Color.R:X2}{picker.Color.G:X2}{picker.Color.B:X2}";
            opacity[index]=1-alpha.Value/100;picker.IsEnabled=system.IsChecked!=true;
            draft.SetAppearanceOptions(profileId,colors[0],colors[1],opacity[0],opacity[1],draft.Find(profileId).ButtonGap);draft.SetVisualStyle(profileId,StyleValue());RefreshPreview();MarkDirty();gradientPanel.Visibility=StyleValue().SkinId=="builtin.color-ring"?Visibility.Visible:Visibility.Collapsed;imageOptions.Visibility=StyleValue().SkinId=="builtin.image"?Visibility.Visible:Visibility.Collapsed;
        }
        state.SelectionChanged+=(_,_)=>Refresh();system.Checked+=(_,_)=>Preview();system.Unchecked+=(_,_)=>Preview();
        picker.ColorChanged+=(_,_)=>Preview();alpha.ValueChanged+=(_,_)=>Preview();
        Refresh();
        var panel=new StackPanel{Spacing=10};panel.Children.Add(state);panel.Children.Add(ColorControl("按钮颜色",picker,system));panel.Children.Add(alpha);
        skin.SelectionChanged+=(_,_)=>Preview();motion.SelectionChanged+=(_,_)=>Preview();
        duration.ValueChanged+=(_,_)=>Preview();highlight.ValueChanged+=(_,_)=>Preview();
        gradientPanel.Visibility=originalStyle.SkinId=="builtin.color-ring"?Visibility.Visible:Visibility.Collapsed;
        gradientStart.ColorChanged+=(_,_)=>Preview();gradientEnd.ColorChanged+=(_,_)=>Preview();angle.ValueChanged+=(_,_)=>Preview();
        imageOptions.Visibility=originalStyle.SkinId=="builtin.image"?Visibility.Visible:Visibility.Collapsed;
        backgroundAlpha.ValueChanged+=(_,_)=>Preview();
        solidOptions.Visibility=originalStyle.SkinId=="builtin.solid"?Visibility.Visible:Visibility.Collapsed;
        solidSystem.Checked+=(_,_)=>{solidColor.IsEnabled=false;Preview();};solidSystem.Unchecked+=(_,_)=>{solidColor.IsEnabled=true;Preview();};solidColor.ColorChanged+=(_,_)=>Preview();solidAlpha.ValueChanged+=(_,_)=>Preview();
        imageOptions.Children.Insert(0,BackgroundPicker(backgroundData,data=>{backgroundData=data;imageNotice.Text=data is null?"尚未选择图片":"已嵌入图片（GIF 自动播放）";Preview();},generation,targetId));
        glassAlpha.Visibility=originalStyle.SkinId=="builtin.glass"?Visibility.Visible:Visibility.Collapsed;glassAlpha.ValueChanged+=(_,_)=>Preview();panel.Children.Add(glassAlpha);panel.Children.Add(new Expander{Header="按钮描边",Content=outlineOptions,HorizontalAlignment=HorizontalAlignment.Stretch});panel.Children.Add(imageOptions);panel.Children.Add(gradientPanel);panel.Children.Insert(0,skin);var animationOptions=new StackPanel{Spacing=8,Children={motion,duration,highlight}};
        panel.Children.Add(new Expander{Header="动画",Content=animationOptions,HorizontalAlignment=HorizontalAlignment.Stretch});
        outlineAuto.IsEnabled=outlineEnabled.IsChecked==true;outlinePicker.IsEnabled=outlineEnabled.IsChecked==true&&outlineAuto.IsChecked!=true;
        outlineEnabled.Checked+=(_,_)=>Preview();outlineEnabled.Unchecked+=(_,_)=>Preview();
        outlineAuto.Checked+=(_,_)=>Preview();outlineAuto.Unchecked+=(_,_)=>Preview();outlinePicker.ColorChanged+=(_,_)=>Preview();
        var replay=new Button{Content="试播出现动画"};
        replay.Click+=(_,_)=>{preview.PlayEntrance();};
        panel.Children.Add(replay);
        var backgroundSection=new StackPanel{Spacing=10};
        var buttonSection=new StackPanel{Spacing=10};
        foreach(var element in new UIElement[]{skin,solidOptions,glassAlpha,imageOptions,gradientPanel}){panel.Children.Remove(element);backgroundSection.Children.Add(element);}
        var buttonColor=panel.Children.OfType<Button>().Single(b=>Equals(b.Tag,"按钮颜色"));
        panel.Children.Remove(state);panel.Children.Remove(buttonColor);panel.Children.Remove(alpha);
        buttonSection.Children.Add(state);
        previewAppearanceRow=new Grid{ColumnSpacing=6};
        previewAppearanceRow.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        previewAppearanceRow.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        previewAppearanceRow.Children.Add(buttonColor);
        buttonSection.Children.Add(previewAppearanceRow);buttonSection.Children.Add(alpha);
        foreach(var expander in panel.Children.OfType<Expander>().ToArray())expander.Content=null;
        panel.Children.Remove(replay);animationOptions.Children.Add(replay);
        appearanceBody.Children.Add(backgroundSection);appearanceBody.Children.Add(buttonSection);
        Border Separator()=>new(){Height=1,Margin=new Thickness(0,8,0,8),Background=new Microsoft.UI.Xaml.Media.SolidColorBrush(global::Windows.UI.Color.FromArgb(50,255,255,255))};
        buttonSection.Children.Add(Separator());buttonSection.Children.Add(outlineOptions);
        var previewLink=new Button{Content="配置窗口预览背景  ›",HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Left};
        previewLink.Click+=async(_,_)=>await RunAsync(ChoosePreviewAppearanceAsync);
        backgroundSection.Children.Add(Separator());
        backgroundSection.Children.Add(new TextBlock{Text="窗口预览背景",FontSize=16,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
        backgroundSection.Children.Add(previewLink);
        animationOptions.Children.Add(Separator());
        animationOptions.Children.Add(new TextBlock{Text="窗口预览动画",FontSize=16,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
        var previewDuration=new Slider{Header="过渡动画时长（毫秒，0 为关闭）",Minimum=0,Maximum=1000,StepFrequency=10,Value=(original.PreviewAppearance??new()).TransitionMilliseconds};
        previewDuration.ValueChanged+=(_,_)=>{if(generation!=appearanceGeneration||targetId!=profileId)return;draft.SetPreviewAppearance(targetId,(draft.Find(targetId).PreviewAppearance??new()) with{TransitionMilliseconds=(int)previewDuration.Value});MarkDirty();};
        animationOptions.Children.Add(previewDuration);
        var previewAnimation=new Button{Content="预览",HorizontalAlignment=HorizontalAlignment.Stretch};
        previewAnimation.Click+=async(_,_)=>await RunAsync(PreviewWindowAppearanceAsync);animationOptions.Children.Add(previewAnimation);
        appearanceBody.Children.Add(animationOptions);appearanceBody.Children.Add(CreateAppearancePresets());
        RefreshAppearancePage();
        foreach(var section in appearanceBody.Children.OfType<FrameworkElement>())section.Loaded+=(_,_)=>AlignColumnContent(section);
    }
}