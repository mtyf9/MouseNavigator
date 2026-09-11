using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor : UserControl
{
    private readonly IReadOnlyList<ActionDescriptor> actions;
    private ConfigurationDraft draft;
    private NavigatorConfiguration saved;
    private string profileId;
    private string? selectedButtonId;
    private bool loading,recording,dialogOpen;
    private readonly ComboBox profiles=new(){MinWidth=210};
    private readonly TextBox processes=new(){Header="应用进程名（多个用分号分隔）",PlaceholderText="例如 Code.exe; devenv.exe"};
    private readonly NumberBox priority=new(){Header="匹配优先级",Minimum=int.MinValue,Maximum=int.MaxValue,Width=120};
    private readonly CheckBox globalDefault=new(){Content="设为全局默认菜单"};
    private readonly ToggleSwitch menuEnabled=new(){OnContent="菜单已启用",OffContent="菜单已禁用"};
    private readonly Button chooseProgram=new(){Content="选择应用程序…"};
    private readonly TextBlock matchWarning=new(){FontSize=12,TextWrapping=TextWrapping.Wrap,Opacity=0.75};
    private readonly ComboBox actionChoice=new(){Header="动作",HorizontalAlignment=HorizontalAlignment.Stretch};
    private readonly TextBox buttonName=new(){Header="名称",MaxLength=80,PlaceholderText="留空使用动作名称"};
    private readonly Button iconPicker=new(){HorizontalAlignment=HorizontalAlignment.Stretch};
    private readonly Button savePreset=new(){Width=48,Height=48,HorizontalAlignment=HorizontalAlignment.Center};
    private readonly StackPanel properties=new(){Spacing=12};
    private readonly StackPanel presetCards=new(){Spacing=8};
    private readonly RadialMenuView preview=new(){IsEditor=true};
    private readonly ComboBox rings=new(){MinWidth=120,PlaceholderText="选择圈"};
    private readonly Viewbox previewBox=new(){HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    private readonly ScrollViewer previewScroll=new(){HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollMode=ScrollMode.Enabled,VerticalScrollMode=ScrollMode.Enabled,ZoomMode=ZoomMode.Disabled};
    private readonly Slider menuSize=new(){Minimum=50,Maximum=200,Value=100,StepFrequency=1,Header="悬浮窗大小",HorizontalAlignment=HorizontalAlignment.Stretch};
    private readonly TextBlock sizeDescription=new(){FontSize=12,Opacity=0.7,TextWrapping=TextWrapping.Wrap};
    private bool addingRing;
    private readonly TextBlock dragHint=new(){Text="拖动插入位置 · 拖到垃圾桶删除 · Esc 取消",FontSize=12,Opacity=0.7,TextWrapping=TextWrapping.Wrap};
    private readonly Button delete=new(){Content="删除菜单"},save=new(){Content="保存并应用"};
    private readonly TextBlock dirtyText=new(){Opacity=0.7,VerticalAlignment=VerticalAlignment.Center};
    private readonly InfoBar feedback=new(){IsClosable=true};
    private readonly StackPanel root=new(){Spacing=14};
    private readonly Canvas dragLayer=new(){IsHitTestVisible=false};
    private readonly Button trash=new(){Width=48,Height=48,Padding=new Thickness(10),CornerRadius=new CornerRadius(10),BorderThickness=new Thickness(1),BorderBrush=Brush(93,67,71),Background=Brush(45,37,45),HorizontalAlignment=HorizontalAlignment.Right};
    private readonly StackPanel centerSettings=new(){Spacing=10,Visibility=Visibility.Collapsed};
    private readonly TextBox centerName=new(){Header="中心文字",MaxLength=40,PlaceholderText="留空不显示文字"};
    private readonly Button centerImagePicker=new(){Content="选择中心图片…"},clearCenterImage=new(){Content="移除图标"};
    private readonly StackPanel shortcutPanel=new(){Spacing=10};
    private readonly TextBox shortcutName=new(){Header="快捷键名称",MaxLength=80};
    private readonly CheckBox ctrl=new(){Content="Ctrl",MinWidth=0},alt=new(){Content="Alt",MinWidth=0},shift=new(){Content="Shift",MinWidth=0},win=new(){Content="Win",MinWidth=0};
    private readonly ComboBox mainKey=new(){Header="主键",HorizontalAlignment=HorizontalAlignment.Stretch};
    private readonly Button record=new(){Content="录入快捷键",HorizontalAlignment=HorizontalAlignment.Stretch};
    private readonly TextBlock chord=new(){FontSize=16,TextWrapping=TextWrapping.Wrap};
    public Func<NavigatorConfiguration,Task>? SaveRequested {get;set;}
    public Func<Task<string?>>? PickImageRequested {get;set;}
    public Func<Task<IReadOnlyList<string>>>? PickProgramsRequested {get;set;}
    public Func<Task<MenuDocument?>>? ImportMenuRequested {get;set;}
    public Func<MenuDocument,Task>? ExportMenuRequested {get;set;}
    public bool IsDirty {get;private set;}
    internal bool HasOpenDialog=>dialogOpen;
    public MenuEditor(NavigatorConfiguration configuration,IReadOnlyList<ActionDescriptor> availableActions)
    {
        saved=configuration;draft=new(configuration);actions=availableActions;
        profileId=draft.Profiles.Single(p=>p.IsDefault).Id;
        var surface=new Grid();surface.Children.Add(root);surface.Children.Add(dragLayer);Content=surface;
        root.Children.Add(new TextBlock {Text="悬浮窗菜单",FontSize=30,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
        var menuTools=new StackPanel {Orientation=Orientation.Horizontal,Spacing=8};
        menuTools.Children.Add(profiles);
        menuTools.Children.Add(MakeButton("重命名",async(_,_)=>await RunAsync(()=>RenameAsync(false))));
        delete.Click+=async(_,_)=>{if(await ConfirmAsync("删除菜单","删除当前悬浮菜单？")){draft.Delete(profileId);profileId=draft.Profiles.Single(p=>p.IsDefault).Id;MarkDirty();RefreshProfiles();}};
        menuTools.Children.Add(delete);
        menuTools.Children.Add(MakeButton("导入菜单",async(_,_)=>await RunAsync(ImportSingleMenuAsync)));
        menuTools.Children.Add(MakeButton("导出菜单",async(_,_)=>await RunAsync(async()=>{if(ExportMenuRequested is not null)await ExportMenuRequested(draft.ExportMenu(profileId));})));
        menuTools.Children.Add(menuEnabled);root.Children.Add(menuTools);
        var saveTools=new StackPanel {Orientation=Orientation.Horizontal,Spacing=8};
        save.Style=(Style)Application.Current.Resources["AccentButtonStyle"];
        save.Click+=async(_,_)=>await RunAsync(SaveDraftAsync);
        saveTools.Children.Add(save);
        saveTools.Children.Add(MakeButton("放弃修改",(_,_)=>Load(saved,false)));
        saveTools.Children.Add(MakeButton("恢复默认",(_,_)=>Load(DefaultProfiles.Configuration() with {Presets=draft.Presets.ToArray()},true)));
        saveTools.Children.Add(dirtyText);root.Children.Add(saveTools);
        var matching=new StackPanel{Spacing=6};
        var matchHeading=new StackPanel{Orientation=Orientation.Horizontal,Spacing=20};
        matchHeading.Children.Add(new TextBlock{Text="应用匹配",FontSize=17,VerticalAlignment=VerticalAlignment.Center});matchHeading.Children.Add(globalDefault);
        matching.Children.Add(matchHeading);
        var matches=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10};processes.Width=330;
        matches.Children.Add(processes);chooseProgram.VerticalAlignment=VerticalAlignment.Bottom;matches.Children.Add(chooseProgram);matches.Children.Add(priority);
        matching.Children.Add(matches);matching.Children.Add(matchWarning);root.Children.Add(matching);
        var editor=new Grid {ColumnSpacing=12};
        editor.ColumnDefinitions.Add(new(){Width=new GridLength(154)});
        editor.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        editor.ColumnDefinitions.Add(new(){Width=new GridLength(236)});
        var library=new Grid{RowSpacing=10};
        library.RowDefinitions.Add(new(){Height=GridLength.Auto});
        library.RowDefinitions.Add(new(){Height=GridLength.Auto});
        library.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        library.Children.Add(new TextBlock {Text="预设按钮",FontSize=18,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
        var libraryHint=new TextBlock {Text="拖入菜单后独立配置",FontSize=12,Opacity=0.65,TextWrapping=TextWrapping.Wrap};
        Grid.SetRow(libraryHint,1);library.Children.Add(libraryHint);
        var presetScroll=new ScrollViewer {Content=presetCards,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        Grid.SetRow(presetScroll,2);library.Children.Add(presetScroll);
        var libraryCard=new Border{Background=Brush(35,40,51),CornerRadius=new CornerRadius(14),Padding=new Thickness(10,14,10,14),Child=library,Height=400,VerticalAlignment=VerticalAlignment.Top};
        editor.Children.Add(libraryCard);
        var designer=new StackPanel {Spacing=10,VerticalAlignment=VerticalAlignment.Top};
        var circleTools=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,0,28)};
        circleTools.Children.Add(rings);
        circleTools.Children.Add(MakeButton("删除圈",async(_,_)=>await RemoveRingAsync()));
        trash.Content=ButtonIcons.Create("\uE74D",null,20);
        ToolTipService.SetToolTip(trash,"拖到垃圾桶删除按钮");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(trash,"拖到垃圾桶删除按钮");



        var previewArea=new Grid();
        previewArea.RowDefinitions.Add(new(){Height=new GridLength(100)});
        previewArea.RowDefinitions.Add(new(){Height=GridLength.Auto});
        previewArea.RowDefinitions.Add(new(){Height=new GridLength(80)});
        previewBox.Child=preview;previewScroll.Content=previewBox;
        Grid.SetRow(previewScroll,1);previewArea.Children.Add(previewScroll);
        previewArea.Children.Add(RotationArea(-1));
        previewArea.Children.Add(RotationArea(1));
        var sizeTools=new StackPanel{MaxWidth=280,Margin=new Thickness(104,0,104,0),Spacing=2,VerticalAlignment=VerticalAlignment.Top};
        sizeDescription.FontSize=11;
        sizeTools.Children.Add(menuSize);sizeTools.Children.Add(sizeDescription);previewArea.Children.Add(sizeTools);
        Grid.SetRow(circleTools,2);previewArea.Children.Add(circleTools);
        var saveArea=new StackPanel{Width=96,Spacing=4,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(4)};
        savePreset.Content=ButtonIcons.Create("\uE74E",null,20);saveArea.Children.Add(savePreset);
        saveArea.Children.Add(new TextBlock{Text="保存为预设按钮",FontSize=11,TextAlignment=TextAlignment.Center});
        var trashArea=new StackPanel{Width=96,Spacing=4,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(4)};
        trash.HorizontalAlignment=HorizontalAlignment.Center;trashArea.Children.Add(trash);
        trashArea.Children.Add(new TextBlock{Text="删除按钮",FontSize=11,TextAlignment=TextAlignment.Center});
        Grid.SetRow(saveArea,2);Grid.SetRow(trashArea,2);
        previewArea.Children.Add(saveArea);previewArea.Children.Add(trashArea);designer.Children.Add(previewArea);

        designer.Children.Add(dragHint);
        centerSettings.Children.Add(new TextBlock{Text="中心按钮",Opacity=0.7});centerSettings.Children.Add(centerName);
        var imageTools=new StackPanel{Spacing=6};imageTools.Children.Add(centerImagePicker);imageTools.Children.Add(clearCenterImage);
        centerSettings.Children.Add(imageTools);
        centerSettings.Children.Add(MakeButton("恢复默认中心",(_,_)=>{draft.SetCenterAppearance(profileId,null,null);MarkDirty();RefreshCenterSettings();RefreshPreview();}));
        Grid.SetColumn(designer,1);editor.Children.Add(designer);
        properties.Children.Add(new TextBlock {Text="配置按钮",FontSize=20,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
        properties.Children.Add(buttonName);properties.Children.Add(iconPicker);properties.Children.Add(actionChoice);
        shortcutPanel.Children.Add(shortcutName);shortcutPanel.Children.Add(record);
        var mods=new Grid();mods.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});mods.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        mods.RowDefinitions.Add(new());mods.RowDefinitions.Add(new());
        var checks=new[]{ctrl,alt,shift,win};for(var i=0;i<checks.Length;i++){Grid.SetColumn(checks[i],i%2);Grid.SetRow(checks[i],i/2);mods.Children.Add(checks[i]);}
        shortcutPanel.Children.Add(mods);shortcutPanel.Children.Add(mainKey);shortcutPanel.Children.Add(chord);
        properties.Children.Add(shortcutPanel);properties.Children.Add(centerSettings);
        var card=new Border {Background=Brush(35,40,51),CornerRadius=new CornerRadius(14),Padding=new Thickness(14),Child=properties};
        properties.VerticalAlignment=VerticalAlignment.Top;
        void SyncLibraryHeight()
        {
            var height=Math.Max(designer.ActualHeight,properties.ActualHeight+28);
            if(height>28&&Math.Abs(libraryCard.Height-height)>0.5)libraryCard.Height=height;
        }
        designer.SizeChanged+=(_,_)=>SyncLibraryHeight();properties.SizeChanged+=(_,_)=>SyncLibraryHeight();
        Grid.SetColumn(card,2);editor.Children.Add(card);root.Children.Add(editor);
        root.Children.Add(feedback);
        PopulateActionChoices();foreach(var pair in ShortcutKeys.MainKeys)mainKey.Items.Add(new ComboBoxItem {Content=pair.Value,Tag=pair.Key});
        profiles.SelectionChanged+=(_,_)=>
        {
            if(loading||profiles.SelectedItem is not ComboBoxItem item)return;
            if((string)item.Tag=="$new") {QueueNewMenu();return;}
            CancelDrag();profileId=(string)item.Tag;selectedButtonId=null;RefreshProfile();
        };
        rings.SelectionChanged+=(_,_)=>{if(!loading&&!addingRing&&rings.SelectedItem is ComboBoxItem item&&(int)item.Tag<0)QueueAddRing((int)item.Tag==-2);};
        menuSize.ValueChanged+=(_,_)=>{
            if(loading||Math.Abs(draft.Find(profileId).SizeScale-menuSize.Value/100)<0.00001)return;
            CancelDrag();draft.SetSize(profileId,menuSize.Value/100);MarkDirty();RefreshPreview();
        };
        previewScroll.ViewChanged+=(_,_)=>{dragWindowToPreview=null;lastDropWindowPoint=null;};
        Loaded+=(_,_)=>RefreshDisplaySize();
        processes.TextChanged+=(_,_)=>UpdateMetadata();priority.ValueChanged+=(_,_)=>UpdateMetadata();
        chooseProgram.Click+=async(_,_)=>await RunAsync(ChooseProgramsAsync);
        globalDefault.Checked+=(_,_)=>{if(loading||draft.Find(profileId).IsDefault)return;draft.SetDefault(profileId);MarkDirty();RefreshProfile();};
        menuEnabled.Toggled+=(_,_)=>{if(loading||draft.Find(profileId).Enabled==menuEnabled.IsOn)return;draft.SetEnabled(profileId,menuEnabled.IsOn);MarkDirty();RefreshMatchWarning();};
        buttonName.TextChanged+=(_,_)=>{if(loading||Entry is not {} e||buttonName.Text==(e.Label??""))return;draft.SetAppearance(profileId,e.Id,buttonName.Text,e.Glyph,e.Image);MarkDirty();RefreshPreview();};
        iconPicker.Click+=async(_,_)=>await ShowIconsAsync();
        centerName.TextChanged+=(_,_)=>{if(loading||centerName.Text==(draft.Find(profileId).CenterText??"松开执行"))return;draft.SetCenterAppearance(profileId,centerName.Text,draft.Find(profileId).CenterImage,draft.Find(profileId).CenterGlyph);MarkDirty();RefreshPreview();};
        centerImagePicker.Click+=async(_,_)=>await ShowIconsAsync();
        clearCenterImage.Click+=(_,_)=>{draft.SetCenterAppearance(profileId,draft.Find(profileId).CenterText,null);MarkDirty();RefreshCenterSettings();RefreshPreview();};
        actionChoice.SelectionChanged+=(_,_)=>ChooseAction();shortcutName.TextChanged+=(_,_)=>UpdateShortcut();
        foreach(var check in checks){check.Checked+=(_,_)=>UpdateShortcut();check.Unchecked+=(_,_)=>UpdateShortcut();}
        mainKey.SelectionChanged+=(_,_)=>UpdateShortcut();
        record.Click+=(_,_)=>StartRecording();recordingTimeout.Tick+=(_,_)=>StopRecording();
        record.LostFocus+=(_,_)=>StopRecording();
        trash.Click+=async(_,_)=>await RemoveButtonAsync();
        savePreset.Click+=(_,_)=>SaveSelectedPreset();
        preview.ButtonSelected+=id=>{selectedButtonId=id;RefreshButton();};
        preview.CenterSelected+=()=>{CancelDrag();selectedButtonId=RadialMenuView.CenterButtonId;RefreshButton();};
        preview.ButtonPressed+=(id,e)=>BeginDrag(id,null,e);
        root.PointerMoved+=DragMoved;root.PointerReleased+=DragReleased;root.PointerCaptureLost+=(_,_)=>CancelDrag();root.PointerCanceled+=(_,_)=>CancelDrag();
        KeyDown+=(_,e)=>{if(e.Key==global::Windows.System.VirtualKey.Escape){CancelDrag();e.Handled=true;}};
        Unloaded+=(_,_)=>{CancelDrag();StopRecording();};
        RefreshPresets();RefreshProfiles();UpdateDirty();
    }
    private MenuEntry? Entry=>draft.Find(profileId).Entries.FirstOrDefault(e=>e.Id==selectedButtonId);
    private void PopulateActionChoices()
    {
        var prior=loading;loading=true;
        try{
            actionChoice.IsDropDownOpen=false;actionChoice.Items.Clear();actionChoice.Items.Add(new ComboBoxItem {Content="不执行动作",Tag=""});
            foreach(var a in actions)actionChoice.Items.Add(new ComboBoxItem {Content=a.Name,Tag=a.Id});
            actionChoice.Items.Add(new ComboBoxItem {Content="自定义快捷键…",Tag="$shortcut"});
            foreach(var id in draft.Profiles.SelectMany(p=>p.Entries).Select(e=>e.ActionId).Distinct().Where(id=>id.Length>0&&draft.Shortcut(id)is null&&actions.All(a=>a.Id!=id)))
                actionChoice.Items.Add(new ComboBoxItem {Content="动作不可用："+id,Tag=id});
        }finally{loading=prior;}
    }
    private bool newMenuPending;
    private void QueueNewMenu()
    {
        if(newMenuPending||dialogOpen)return;newMenuPending=true;profiles.IsDropDownOpen=false;
        DispatcherQueue.TryEnqueue(async()=>
        {
            try
            {
                loading=true;profiles.SelectedItem=profiles.Items.Cast<ComboBoxItem>().FirstOrDefault(i=>(string)i.Tag==profileId);loading=false;
                await RunAsync(()=>RenameAsync(true));
            }
            catch(Exception ex){Notify(ex.Message,InfoBarSeverity.Error);}
            finally {loading=false;newMenuPending=false;}
        });
    }
    private void RefreshProfiles()
    {
        loading=true;
        try
        {
            profiles.IsDropDownOpen=false;profiles.Items.Clear();
            foreach(var p in draft.Profiles){var item=new ComboBoxItem{Content=p.Name,Tag=p.Id};profiles.Items.Add(item);if(p.Id==profileId)profiles.SelectedItem=item;}
            profiles.Items.Add(new ComboBoxItem{Content="＋新建悬浮菜单",Tag="$new"});
        }
        finally{loading=false;}
        RefreshProfile();
    }
    private void RefreshProfile()
    {
        var p=draft.Find(profileId);loading=true;
        try
        {
            processes.Text=string.Join("; ",p.Applications.Select(a=>a.ProcessName));
            processes.IsEnabled=chooseProgram.IsEnabled=priority.IsEnabled=!p.IsDefault;
            globalDefault.IsChecked=p.IsDefault;globalDefault.IsEnabled=!p.IsDefault;
            menuEnabled.IsOn=p.Enabled;priority.Value=p.Priority;delete.IsEnabled=!p.IsDefault;menuSize.Value=p.SizeScale*100;
        }
        finally{loading=false;}
        RefreshCenterSettings();RefreshRings(0);RefreshButton();RefreshMatchWarning();
    }
    private void RefreshRings(int selected)
    {
        var prior=loading;loading=true;
        try
        {
            rings.IsDropDownOpen=false;rings.Items.Clear();
            var count=draft.Find(profileId).RingCount;
            rings.Items.Add(new ComboBoxItem{Content="＋内圈",Tag=-2});
            for(var i=0;i<count;i++)rings.Items.Add(new ComboBoxItem{Content=$"第 {i+1} 圈",Tag=i});
            rings.Items.Add(new ComboBoxItem{Content="＋外圈",Tag=-1});
            rings.SelectedIndex=count==0?-1:Math.Clamp(selected,0,count-1)+1;
        }
        finally{loading=prior;}
    }
    private void RefreshButton()
    {
        loading=true;
        try{
            var centerSelected=selectedButtonId==RadialMenuView.CenterButtonId;
            centerSettings.Visibility=centerSelected?Visibility.Visible:Visibility.Collapsed;
            foreach(var control in new UIElement[]{buttonName,iconPicker,actionChoice})
                control.Visibility=centerSelected?Visibility.Collapsed:Visibility.Visible;
            if(centerSelected){trash.IsEnabled=savePreset.IsEnabled=false;StopRecording();shortcutPanel.Visibility=Visibility.Collapsed;RefreshCenterSettings();RefreshPreview();return;}
            var e=Entry;if(e is null){selectedButtonId=draft.Find(profileId).Entries.FirstOrDefault()?.Id;e=Entry;}
            buttonName.IsEnabled=iconPicker.IsEnabled=actionChoice.IsEnabled=trash.IsEnabled=savePreset.IsEnabled=e is not null;
            buttonName.Text=e?.Label??"";
            var icon=new StackPanel {Orientation=Orientation.Horizontal,Spacing=8};icon.Children.Add(ButtonIcons.Create(e?.Glyph??actions.FirstOrDefault(a=>a.Id==e?.ActionId)?.Glyph,e?.Image));icon.Children.Add(new TextBlock{Text="选择图标 / 图片",VerticalAlignment=VerticalAlignment.Center});iconPicker.Content=icon;
            var id=draft.Shortcut(e?.ActionId)is not null?"$shortcut":e?.ActionId??"";
            actionChoice.SelectedItem=actionChoice.Items.Cast<ComboBoxItem>().FirstOrDefault(i=>(string)i.Tag==id);
            if(e is not null)rings.SelectedItem=rings.Items.Cast<ComboBoxItem>().First(i=>(int)i.Tag==e.Ring);
            RefreshShortcutFields();
        }finally{loading=false;}
    }
    private void RefreshShortcutFields()
    {
        StopRecording();var prior=loading;loading=true;
        try{
            var shortcut=draft.Shortcut(Entry?.ActionId);shortcutPanel.Visibility=shortcut is null?Visibility.Collapsed:Visibility.Visible;
            shortcutName.Text=shortcut?.Name??"自定义快捷键";var keys=shortcut?.Keys??new ushort[]{17,75};
            ctrl.IsChecked=keys.Contains((ushort)17);alt.IsChecked=keys.Contains((ushort)18);shift.IsChecked=keys.Contains((ushort)16);win.IsChecked=keys.Contains((ushort)91);
            mainKey.SelectedItem=mainKey.Items.Cast<ComboBoxItem>().First(i=>(ushort)i.Tag==keys[^1]);chord.Text=ShortcutKeys.Format(keys);
        }finally{loading=prior;}RefreshPreview();RefreshIcon();
    }
    private void RefreshPreview(MenuProfile? proposal=null)
    {
        preview.SetMenu(proposal??draft.Find(profileId),id=>draft.Shortcut(id)is{}s?new ActionDescriptor(id,s.Name,"\uE765",""):actions.FirstOrDefault(a=>a.Id==id));
        preview.Highlight(selectedButtonId);RefreshDisplaySize();
    }
    private void RefreshIcon()
    {
        var e=Entry;var visual=preview.Buttons.FirstOrDefault(b=>b.Id==e?.Id);
        var icon=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};
        icon.Children.Add(ButtonIcons.Create(visual?.Glyph,e?.Image));
        icon.Children.Add(new TextBlock{Text="选择图标 / 图片",VerticalAlignment=VerticalAlignment.Center});iconPicker.Content=icon;
    }
    private void UpdateMetadata()
    {
        if(loading)return;var p=draft.Find(profileId);
        var matches=p.IsDefault?p.Applications:processes.Text.Split([';','；',','],StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select(v=>new ApplicationMatch(v)).ToArray();
        var value=double.IsNaN(priority.Value)?0:(int)priority.Value;
        if(p.Applications.SequenceEqual(matches)&&p.Priority==value)return;
        draft.Update(profileId,p.Name,matches,value);MarkDirty();RefreshMatchWarning();
    }
}