#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MouseNavigator.Core;
using MouseNavigator.Contracts;
using System.Runtime.InteropServices.WindowsRuntime;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    private static IEnumerable<FrameworkElement> AppearanceControls(FrameworkElement element)
    {
        yield return element;
        IEnumerable<FrameworkElement> children=element switch
        {
            Panel panel=>panel.Children.OfType<FrameworkElement>(),
            Button {Flyout:Flyout flyout} when flyout.Content is FrameworkElement child=>[child],
            ContentControl {Content:FrameworkElement child}=>[child],
            Border {Child:FrameworkElement child}=>[child],
            _=>[]
        };
        foreach(var child in children)foreach(var item in AppearanceControls(child))yield return item;
    }
    internal async Task CheckAppearanceAsync(List<string> results,string directory)
    {
        void Check(bool ok,string name)=>results.Add((ok?"PASS: ":"FAIL: ")+name);
        var before=draft.Snapshot();
        var idleTrash=trash.Background;var idleSave=savePreset.Background;
        ClearButtonSelection();var clearedTrash=trash.Background;var clearedSave=savePreset.Background;
        ClearButtonSelection();CancelDrag();
        Check(ReferenceEquals(clearedTrash,trash.Background)&&ReferenceEquals(clearedSave,savePreset.Background),"Repeated blank clicks keep toolbar brushes stable");
        var bar=Descendants(this).OfType<Microsoft.UI.Xaml.Controls.Primitives.ScrollBar>().FirstOrDefault(b=>b.IndicatorMode==Microsoft.UI.Xaml.Controls.Primitives.ScrollingIndicatorMode.MouseIndicator&&b.Maximum>0);
        Check(bar is not null&&bar.Visibility==Visibility.Visible&&bar.ActualHeight>0,"Overflowing sidebar has a visible mouse scrollbar");
        var controls=AppearanceControls(appearanceBody).ToArray();
        var style=controls.OfType<ComboBox>().Single(c=>Equals(c.Header,"外观样式"));
        var outline=controls.OfType<CheckBox>().Single(c=>Equals(c.Content,"显示按钮描边"));
        style.SelectedIndex=MenuSkins.Available.ToList().FindIndex(s=>s.Id=="builtin.color-ring");
        outline.IsChecked=false;
        Check(!dialogOpen&&IsDirty&&draft.Find(profileId).VisualStyle is {OutlineEnabled:false,SkinId:"builtin.color-ring"},"Sidebar changes draft immediately without dialog");
        outline.IsChecked=true;
        controls.OfType<RadioButton>().Single(c=>Equals(c.Content,"自动选择描边颜色")).IsChecked=false;
        var colorButton=controls.OfType<Button>().Single(b=>Equals(b.Tag,"描边颜色"));
        AppearanceControls((FrameworkElement)((Flyout)colorButton.Flyout).Content).OfType<ColorPicker>().Single().Color=global::Windows.UI.Color.FromArgb(255,20,160,220);
        Check(draft.Find(profileId).VisualStyle?.OutlineColor=="#14A0DC","Custom outline color stored");
        Check(controls.OfType<Button>().Count(b=>b.Flyout is Flyout)==5,"All five color palettes live in flyouts");
        var colorPanel=(FrameworkElement)((Flyout)controls.OfType<Button>().Single(b=>Equals(b.Tag,"按钮颜色")).Flyout).Content;
        var colorOptions=AppearanceControls(colorPanel).OfType<RadioButton>().ToArray();
        var autoColor=colorOptions.Single(r=>Equals(r.Content,"跟随系统主题色"));
        var customColor=colorOptions.Single(r=>Equals(r.Content,"自定义颜色"));
        customColor.IsChecked=true;
        Check(autoColor.IsChecked==false&&draft.Find(profileId).AccentColor is not null,"Custom button color excludes system color");
        autoColor.IsChecked=true;
        Check(customColor.IsChecked==false&&draft.Find(profileId).AccentColor is null,"System color excludes custom color");
        var outlinePanel=(FrameworkElement)((Flyout)colorButton.Flyout).Content;
        var outlineModes=AppearanceControls(outlinePanel).OfType<RadioButton>().ToArray();
        outlineModes.Single(r=>Equals(r.Content,"自动选择描边颜色")).IsChecked=true;
        Check(outlineModes.Single(r=>Equals(r.Content,"自定义颜色")).IsChecked==false&&draft.Find(profileId).VisualStyle?.OutlineColor is null,"Outline modes are exclusive");
        var tabs=Descendants(this).OfType<SlidingTabs>().ToArray();
        Check(tabs.Length==2,"Both columns use text tabs");
        tabs[1].SelectedIndex=2;await Task.Delay(240);
        Check(appearanceBody.Children[2].Visibility==Visibility.Visible&&appearanceBody.Children[0].Visibility==Visibility.Collapsed,"Right tabs switch flat appearance pages");
        tabs[1].SelectedIndex=0;
        var restored=MenuDocumentCodec.Deserialize(MenuDocumentCodec.Serialize(draft.ExportMenu(profileId)));
        Check(restored.Menu.VisualStyle==draft.Find(profileId).VisualStyle,"Outline and gradient export together");
        var gradient=new AngularBackgroundView();gradient.Update(new("builtin.color-ring",GradientStart:"#FF0000",GradientEnd:"#0000FF"));
        var bitmap=gradient.BitmapForSmoke!;var bytes=new byte[512*512*4];using(var stream=bitmap.PixelBuffer.AsStream())stream.ReadExactly(bytes);
        Check(bytes[(256*512+256)*4+3]==255,"Angular gradient fills center");
        Check(Math.Abs(bytes[(256*512+330)*4+2]-bytes[(256*512+450)*4+2])<=1
            &&bytes[(256*512+450)*4+2]>240&&bytes[(256*512+60)*4]>240,"Gradient follows angle instead of linear position");
        await Task.Delay(100);await SmokeScenario.RenderAsync((FrameworkElement)XamlRoot.Content,Path.Combine(directory,"angular-gradient.png"));buttonPageChanged?.Invoke(false);
        Check(buttonConfigurationScroll is ScrollViewer {Visibility:Visibility.Collapsed},"Preset tab hides button form");
        preview.SelectAt(0,0);
        Check(buttonConfigurationScroll is ScrollViewer {Visibility:Visibility.Visible}&&selectedButtonId==RadialMenuView.CenterButtonId,"Center click opens configuration tab");
        ClearButtonSelection();RefreshButton();
        Check(selectedButtonId is null&&!trash.IsEnabled&&!savePreset.IsEnabled,"Blank selection remains cleared after refresh");
        Check(appearanceBody.Children.Count==4&&AppearanceControls((FrameworkElement)appearanceBody.Children[1]).Contains(outline),"Outline belongs to colors page");
        style.SelectedIndex=MenuSkins.Available.ToList().FindIndex(s=>s.Id=="builtin.image");
        var backgrounds=controls.OfType<ComboBox>().Single(c=>Equals(c.Header,"图片预设"));
        backgrounds.SelectedIndex=1;await Task.Delay(200);
        var embedded=draft.Find(profileId).VisualStyle!.BackgroundImage;
        Check(embedded is not null&&MenuDocumentCodec.Deserialize(MenuDocumentCodec.Serialize(draft.ExportMenu(profileId))).Menu.VisualStyle!.BackgroundImage==embedded,"Selected sample image travels with exported menu");
        var oldPicker=PickBackgroundRequested;var beforeCancel=ConfigurationCodec.Serialize(draft.Snapshot());var canceled=0;
        try
        {
            PickBackgroundRequested=()=>{canceled++;return Task.FromResult<string?>(null);};
            for(var attempt=0;attempt<2;attempt++)
            {
                backgrounds.IsDropDownOpen=true;await Task.Delay(80);
                backgrounds.SelectedIndex=backgrounds.Items.Count-1;await Task.Delay(400);
            }
            Check(canceled==2&&!dialogOpen&&backgrounds.IsEnabled&&ConfigurationCodec.Serialize(draft.Snapshot())==beforeCancel
                &&Equals(((ComboBoxItem)backgrounds.SelectedItem).Tag,embedded),"Repeated canceled imports preserve selection and configuration");
        }
        finally{PickBackgroundRequested=oldPicker;}
        var sampleId=draft.BackgroundPresets[0].Id;
        backgrounds.IsDropDownOpen=true;await Task.Delay(80);
        var sampleRow=(Grid)((ComboBoxItem)backgrounds.Items[1]).Content;
        var removeSample=sampleRow.Children.OfType<Button>().Single();
        ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(removeSample).GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();
        await Task.Delay(180);
        Check(draft.Find(profileId).VisualStyle!.BackgroundImage==embedded&&!new ConfigurationDraft(draft.Snapshot()).BackgroundPresets.Any(p=>p.Id==sampleId),"Deleting a background preset persists and keeps applied image");
        var importedId=draft.AddBackgroundPreset(embedded!);
        Check(new ConfigurationDraft(ConfigurationCodec.Deserialize(ConfigurationCodec.Serialize(draft.Snapshot()))).BackgroundPresets.Any(p=>p.Id==importedId&&p.Image==embedded),"Imported background survives backup");
        style.SelectedIndex=0;
        var solid=controls.OfType<Button>().Single(b=>Equals(b.Tag,"背景颜色"));
        var solidControls=AppearanceControls((FrameworkElement)((Flyout)solid.Flyout).Content).ToArray();
        solidControls.OfType<RadioButton>().Single(r=>Equals(r.Content,"自定义颜色")).IsChecked=true;
        solidControls.OfType<ColorPicker>().Single().Color=global::Windows.UI.Color.FromArgb(255,20,90,140);
        controls.OfType<Slider>().Single(s=>Equals(s.Header,"背景透明度（0% 不透明，100% 透明）")).Value=45;
        Check(draft.Find(profileId).VisualStyle is {SolidColor:"#145A8C",SolidOpacity:var a}&&Math.Abs(a-.55)<.001,"Solid background color and opacity update draft");
        solidControls.OfType<RadioButton>().Single(r=>Equals(r.Content,"跟随系统主题色")).IsChecked=true;
        Check(draft.Find(profileId).VisualStyle!.SolidColor is null&&!solidControls.OfType<ColorPicker>().Single().IsEnabled,"Solid background can follow system accent");
        tabs[1].SelectedIndex=1;buttonPageChanged?.Invoke(false);await Task.Delay(150);
        await SmokeScenario.RenderAsync((FrameworkElement)XamlRoot.Content,Path.Combine(directory,"button-sidebar.png"));
        var appearanceBefore=MenuAppearance.From(draft.Find(profileId));
        draft.SaveAppearancePreset(profileId,"外观检查");var appearanceId=draft.AppearancePresets.Last().Id;
        draft.SetVisualStyle(profileId,new("builtin.glass"));draft.ApplyAppearancePreset(profileId,appearanceId);
        Check(MenuAppearance.From(draft.Find(profileId))==appearanceBefore,"Appearance preset restores all appearance properties");
        var restoredAppearance=new ConfigurationDraft(ConfigurationCodec.Deserialize(ConfigurationCodec.Serialize(draft.Snapshot())));
        Check(restoredAppearance.AppearancePresets.Any(p=>p.Id==appearanceId),"Appearance preset survives backup");
        tabs[1].SelectedIndex=3;
        Check(appearanceBody.Children.All(c=>c.Visibility==Visibility.Collapsed)&&appearanceLayout.Visibility==Visibility.Visible,"Layout page excludes appearance presets");
        tabs[1].SelectedIndex=4;
        Check(appearanceBody.Children[3].Visibility==Visibility.Visible&&appearanceLayout.Visibility==Visibility.Collapsed,"Appearance presets have their own tab");
        var menuBeforeCancel=profileId;
        DialogOpenedForSmoke=async dialog=>{
            await Task.Delay(100);
            var button=Descendants(dialog).OfType<Button>().Single(b=>b.Name=="CloseButton");
            ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(button).GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();
        };
        try
        {
            profiles.SelectedItem=profiles.Items.Cast<ComboBoxItem>().Single(i=>Equals(i.Tag,"$new"));
            for(var i=0;i<60&&newMenuPending;i++)await Task.Delay(50);
            Check(profileId==menuBeforeCancel&&Equals(((ComboBoxItem)profiles.SelectedItem).Tag,menuBeforeCancel),"Continue editing after new menu restores current menu selection");
        }
        finally{DialogOpenedForSmoke=null;}
        var originalSave=SaveRequested;NavigatorConfiguration? persisted=null;
        try
        {
            SaveRequested=value=>{persisted=value;return Task.CompletedTask;};
            var source=MenuAppearance.From(draft.Find(profileId));
            var list=saved.AppearancePresets?.ToList()??[];list.Add(new("independent-appearance","独立保存检查",source));
            var dirtyBefore=IsDirty;
            await PersistAppearancePresetsAsync(list);
            Check(persisted is not null&&MenuAppearance.From(persisted.Profiles.Single(p=>p.Id==profileId))!=source&&IsDirty==dirtyBefore,"Saving appearance library does not save menu edits");
            var replacement=source with{SizeScale=1.25};list[^1]=list[^1] with{Appearance=replacement};
            await PersistAppearancePresetsAsync(list);
            Load(saved,false);
            Check(draft.AppearancePresets.Single(p=>p.Id=="independent-appearance").Appearance==replacement,"Overwritten preset survives discarding menu edits");
            SaveRequested=_=>throw new IOException("保存失败");
            var stored=saved;
            try{await PersistAppearancePresetsAsync([]);}catch(IOException){}
            Check(saved==stored&&draft.AppearancePresets.Any(p=>p.Id=="independent-appearance"),"Failed preset save preserves persisted library");
        }
        finally{SaveRequested=originalSave;}
        Load(before,false);
        Check(draft.Find(profileId).VisualStyle==before.Profiles.Single(p=>p.Id==profileId).VisualStyle,"Discard restores appearance sidebar");
        PreviewOpenedForSmoke=async(window,body)=>{
            await Task.Delay(180);
            var previewControls=AppearanceControls(body).ToArray();
            var colors=previewControls.OfType<Button>().Where(b=>b.Tag is string tag&&tag.StartsWith("preview-color-")).ToArray();
            var follow=previewControls.OfType<CheckBox>().Single(c=>Equals(c.Content,"跟随悬浮窗背景样式和配色"));
            follow.IsChecked=true;Check(colors.Length==4&&colors.All(c=>!c.IsEnabled),"Following background disables all four color rows");
            follow.IsChecked=false;
            var count=window.ThumbnailCountForSmoke;
            foreach(var button in colors)
            {
                var peer=new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(button);
                ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)peer.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();
                await Task.Delay(80);
            }
            Check(count>0&&window.ThumbnailCountForSmoke==count&&window.ThumbnailOpacityForSmoke==255,"All color rows open without losing native thumbnails");
            var area=Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(window.AppWindow.Id,Microsoft.UI.Windowing.DisplayAreaFallback.Nearest).WorkArea;
            Check(window.AppWindow.Position.X>=area.X&&window.AppWindow.Position.X+window.AppWindow.Size.Width<=area.X+area.Width,"Expanded color editor stays inside screen width");

            Check(!previewControls.OfType<Slider>().Any(s=>s.Header?.ToString()?.Contains("过渡动画")==true),"Preview background dialog excludes animation settings");
            window.CloseAppearanceEditor();
        };
        try{await ChoosePreviewAppearanceAsync();}finally{PreviewOpenedForSmoke=null;}
        PreviewOpenedForSmoke=async(window,body)=>{
            await Task.Delay(500);
            Check(window.ThumbnailCountForSmoke>0&&window.ThumbnailOpacityForSmoke==255,"Preview-only animation preserves native thumbnails after layout");
            var presenter=(Microsoft.UI.Windowing.OverlappedPresenter)window.AppWindow.Presenter;
            Check(!presenter.HasTitleBar&&!presenter.HasBorder,"Animation preview has no title bar or border");
            window.CloseAppearanceEditor();
        };
        try{await PreviewWindowAppearanceAsync();}finally{PreviewOpenedForSmoke=null;}
        GC.Collect();GC.WaitForPendingFinalizers();await Task.Delay(1500);
        buttonPageChanged?.Invoke(false);await Task.Delay(100);await SmokeScenario.RenderAsync((FrameworkElement)XamlRoot.Content,Path.Combine(directory,"sidebar.png"));
    }
}
#endif
