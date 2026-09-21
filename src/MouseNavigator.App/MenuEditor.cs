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
    private readonly TextBox processes=new(){PlaceholderText="应用进程，例如 Code.exe; devenv.exe"};
    private readonly NumberBox priority=new(){PlaceholderText="优先级",Minimum=int.MinValue,Maximum=int.MaxValue,Width=120};
    private readonly CheckBox globalDefault=new(){Content="设为全局默认菜单"};
    private readonly ToggleSwitch menuEnabled=new(){OnContent="菜单已启用",OffContent="菜单已禁用"};
    private readonly Button chooseProgram=new(){Content="选择应用程序…"};
    private readonly TextBlock matchWarning=new(){FontSize=12,TextWrapping=TextWrapping.Wrap,Opacity=0.75};
    private readonly StackPanel actionChoices=new(){Spacing=6};
    private readonly TextBox buttonName=new(){Header="名称",MaxLength=80,PlaceholderText="留空使用动作名称"};
    private readonly Button iconPicker=new(){HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Left};
    private readonly Button savePreset=new(){Width=48,Height=48,HorizontalAlignment=HorizontalAlignment.Center};
    private ScrollViewer? buttonConfigurationScroll; private Action<bool>? buttonPageChanged; private readonly StackPanel properties=new(){Spacing=12};
    private readonly TextBox presetSearch=new(){PlaceholderText="搜索预设按钮",Margin=new Thickness(0,6,0,8)};
    private readonly StackPanel presetCards=new(){Spacing=8};
    private readonly RadialMenuView preview=new(){IsEditor=true};
    private readonly ComboBox rings=new(){MinWidth=120,PlaceholderText="选择圈"};
    private readonly Viewbox previewBox=new(){HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    private readonly ScrollViewer previewScroll=new(){HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollMode=ScrollMode.Enabled,VerticalScrollMode=ScrollMode.Enabled,ZoomMode=ZoomMode.Disabled};
    private readonly Slider buttonGap=new(){Header="按钮间隙（度）",Minimum=0,Maximum=20,StepFrequency=1};
    private readonly Slider menuSize=new(){Minimum=50,Maximum=200,Value=100,StepFrequency=1,Header="悬浮窗大小",HorizontalAlignment=HorizontalAlignment.Stretch};
    private readonly TextBlock sizeDescription=new(){FontSize=12,Opacity=0.7,TextWrapping=TextWrapping.Wrap};
    private bool addingRing;
    private readonly TextBlock dragHint=new(){Text="拖动插入位置 · 拖到垃圾桶删除 · Esc 取消",FontSize=12,Opacity=0.7,TextWrapping=TextWrapping.Wrap};
    private readonly Button delete=new(){Content="删除菜单"},save=new(){Content="保存并应用"};
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
    public Func<Task<string?>>? PickBackgroundRequested {get;set;} public Func<Task<string?>>? PickImageRequested {get;set;}
    public Func<Task<IReadOnlyList<string>>>? PickProgramsRequested {get;set;}
    public Func<Task<MenuDocument?>>? ImportMenuRequested {get;set;}
    public Func<MenuDocument,Task>? ExportMenuRequested {get;set;}
    public Action? OpenSettingsRequested {get;set;}
    public bool IsDirty {get;private set;}
    internal bool HasOpenDialog=>dialogOpen;
    public MenuEditor(NavigatorConfiguration configuration,IReadOnlyList<ActionDescriptor> availableActions)
    {
        InitializePanelTheme();
        feedback.Closing+=(_,args)=>{args.Cancel=true;ClearNotification();};
        ToolTipService.SetToolTip(previewAppearanceButton,"窗口预览外观");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(previewAppearanceButton,"窗口预览外观");
        previewAppearanceButton.Click+=async(_,_)=>await RunAsync(ChoosePreviewAppearanceAsync);

        IsDirty=!configuration.BuiltInMenusInitialized;configuration=DefaultProfiles.InitializeMenus(configuration);
        saved=configuration;draft=new(configuration);actions=availableActions;
        presetBack.Click+=(_,_)=>NavigatePresetFolder(currentPresetFolder?.StartsWith("$")==true?null:draft.PresetFolders.FirstOrDefault(f=>f.Id==currentPresetFolder)?.ParentId);
        profileId=draft.Profiles.Single(p=>p.IsDefault).Id;
        var surface=new Grid();surface.Children.Add(root);surface.Children.Add(dragLayer);Content=surface;
        root.Children.Add(new TextBlock {Text="悬浮窗菜单",FontSize=30,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
        var menuTools=new StackPanel {Orientation=Orientation.Horizontal,Spacing=8};
        menuTools.Children.Add(profiles);menuTools.Children.Add(menuEnabled);root.Children.Add(menuTools);
        var saveTools=new StackPanel {Orientation=Orientation.Horizontal,Spacing=8};
        save.Style=(Style)Application.Current.Resources["AccentButtonStyle"];
        save.Click+=async(_,_)=>await RunAsync(SaveDraftAsync);
        saveTools.Children.Add(save);
        saveTools.Children.Add(MakeButton("放弃修改",(_,_)=>Load(saved,false)));


        saveTools.Children.Add(MakeButton("导入菜单",async(_,_)=>await RunAsync(ImportSingleMenuAsync)));
        saveTools.Children.Add(MakeButton("导出菜单",async(_,_)=>await RunAsync(async()=>{if(ExportMenuRequested is not null)await ExportMenuRequested(draft.ExportMenu(profileId));})));
        menuTools.Children.Add(saveTools);

        var matching=new StackPanel{Spacing=6};
        var matchHeading=new StackPanel{Orientation=Orientation.Horizontal,Spacing=20};
        matchHeading.Children.Add(new TextBlock{Text="应用匹配",FontSize=17,VerticalAlignment=VerticalAlignment.Center});matchHeading.Children.Add(globalDefault);

        var matches=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10};processes.Width=330;
        matches.Children.Add(matchHeading);matches.Children.Add(processes);chooseProgram.VerticalAlignment=VerticalAlignment.Bottom;matches.Children.Add(chooseProgram);matches.Children.Add(new TextBlock{Text="优先级",VerticalAlignment=VerticalAlignment.Center});matches.Children.Add(priority);
        matching.Children.Add(matches);matching.Children.Add(matchWarning);root.Children.Add(matching);
        var editor=new Grid {ColumnSpacing=12};
        editor.ColumnDefinitions.Add(new(){Width=new GridLength(290)});
        editor.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        editor.ColumnDefinitions.Add(new(){Width=new GridLength(290)});
        var library=new Grid{RowSpacing=10};
        library.RowDefinitions.Add(new(){Height=GridLength.Auto});
        library.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});

        var libraryHint=new TextBlock {Text="拖入菜单后独立配置",FontSize=12,Opacity=0.65,TextWrapping=TextWrapping.Wrap};
        var searchArea=new StackPanel();searchArea.Children.Add(libraryHint);searchArea.Children.Add(presetSearch);
        var folderTools=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6};
        folderTools.Children.Add(presetBack);folderTools.Children.Add(MakeButton("新建文件夹",async(_,_)=>await CreatePresetFolderAsync()));
        Grid.SetRow(searchArea,0);library.Children.Add(searchArea);presetSearch.TextChanged+=(_,_)=>{if(!presetNavigationPending)RefreshPresets();};
        presetBack.Padding=new Thickness(8,6,8,6);
        foreach(var tool in folderTools.Children.OfType<Button>()){tool.Padding=new Thickness(8,6,8,6);tool.FontSize=12;tool.HorizontalContentAlignment=HorizontalAlignment.Left;}
        var presetHost=SidebarScroll(new StackPanel{Spacing=10,Children={presetBreadcrumb,folderTools,presetCards}},presetScroll);
        Grid.SetRow(presetHost,1);library.Children.Add(presetHost);
        var libraryCard=new Border{Background=panelThemeBrush,CornerRadius=new CornerRadius(14),Padding=new Thickness(18),Child=library,Height=400,VerticalAlignment=VerticalAlignment.Top};

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

        properties.Children.Add(buttonName);properties.Children.Add(iconPicker);properties.Children.Add(actionChoices);
        shortcutPanel.Children.Add(shortcutName);shortcutPanel.Children.Add(record);
        var mods=new Grid();mods.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});mods.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        mods.RowDefinitions.Add(new());mods.RowDefinitions.Add(new());
        var checks=new[]{ctrl,alt,shift,win};for(var i=0;i<checks.Length;i++){Grid.SetColumn(checks[i],i%2);Grid.SetRow(checks[i],i/2);mods.Children.Add(checks[i]);}
        var manual=new Expander{Header="手动选择（可选）",HorizontalAlignment=HorizontalAlignment.Stretch};
        var manualBody=new StackPanel{Spacing=8};manualBody.Children.Add(mods);manualBody.Children.Add(mainKey);manual.Content=manualBody;
        shortcutPanel.Children.Add(chord);shortcutPanel.Children.Add(manual);
        properties.Children.Add(centerSettings);
        var propertyScroll=new ScrollViewer{Visibility=Visibility.Collapsed,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        var propertyHost=SidebarScroll(properties,propertyScroll);buttonConfigurationScroll=propertyScroll;
        var tabs=new SlidingTabs("预设","配置");
        buttonPageChanged=configure=>{configure=configure&&selectedButtonId is not null;
            library.Visibility=configure?Visibility.Collapsed:Visibility.Visible;
            propertyScroll.Visibility=configure?Visibility.Visible:Visibility.Collapsed;
            tabs.SelectedIndex=configure?1:0;
        };
        tabs.SelectionChanged+=index=>buttonPageChanged(index==1);
        libraryCard.Child=null;
        var left=new Grid{RowSpacing=10};
        left.RowDefinitions.Add(new(){Height=GridLength.Auto});left.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        left.Children.Add(new StackPanel{Spacing=8,Children={new TextBlock{Text="功能",FontSize=24,FontWeight=Microsoft.UI.Text.FontWeights.Bold},tabs}});Grid.SetRow(library,1);Grid.SetRow(propertyHost,1);left.Children.Add(library);left.Children.Add(propertyHost);
        libraryCard.Child=left;editor.Children.Add(libraryCard);
        appearanceLayout.Children.Add(menuSize);appearanceLayout.Children.Add(sizeDescription);appearanceLayout.Children.Add(buttonGap);
        var rightContent=new StackPanel{Spacing=10,Children={appearanceBody,appearanceLayout}};
        var rightScroll=new ScrollViewer{HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        var rightHost=SidebarScroll(rightContent,rightScroll);var appearanceTabs=new SlidingTabs("背景","按钮","动画","布局","预设");
        appearanceTabs.SelectionChanged+=index=>{appearancePage=index;RefreshAppearancePage();};
        var rightGrid=new Grid{RowSpacing=12};
        rightGrid.RowDefinitions.Add(new(){Height=GridLength.Auto});rightGrid.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        rightGrid.Children.Add(new StackPanel{Spacing=8,Children={new TextBlock{Text="外观",FontSize=24,FontWeight=Microsoft.UI.Text.FontWeights.Bold},appearanceTabs}});Grid.SetRow(rightHost,1);rightGrid.Children.Add(rightHost);
        var right=new Border{Background=panelThemeBrush,CornerRadius=new(14),Padding=new(18),Child=rightGrid,VerticalAlignment=VerticalAlignment.Top};
        Grid.SetColumn(right,2);editor.Children.Add(right);root.Children.Add(editor);
        void ResizeSides()
        {
            var height=Math.Max(360,(XamlRoot?.Size.Height??850)-260);
            libraryCard.Height=height;right.Height=height;rightScroll.MaxHeight=height-112;
        }
        SizeChanged+=(_,_)=>ResizeSides();Loaded+=(_,_)=>ResizeSides();
        root.Children.Add(feedback);
        BuildLaunchControls();PopulateActionChoices();foreach(var pair in ShortcutKeys.MainKeys)mainKey.Items.Add(new ComboBoxItem {Content=pair.Value,Tag=pair.Key});
        profiles.DropDownOpened+=(_,_)=>SetProfileRowTools(true);
        profiles.DropDownClosed+=(_,_)=>DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,()=>{if(!profiles.IsDropDownOpen)SetProfileRowTools(false);});
        profiles.SelectionChanged+=(_,_)=>
        {
            if(loading||profiles.SelectedItem is not ComboBoxItem item)return;
            if((string)item.Tag=="$new") {QueueNewMenu();return;}

            QueueProfileSelection((string)item.Tag);
        };
        rings.SelectionChanged+=(_,_)=>{if(!loading&&!addingRing&&rings.SelectedItem is ComboBoxItem item&&(int)item.Tag<0)QueueAddRing((int)item.Tag==-2);};
        buttonGap.ValueChanged+=(_,_)=>{if(loading)return;var p=draft.Find(profileId);if(Math.Abs(p.ButtonGap-buttonGap.Value)<.001)return;draft.SetAppearanceOptions(profileId,p.AccentColor,p.NormalColor,p.ActiveOpacity,p.NormalOpacity,buttonGap.Value);MarkDirty();RefreshPreview();};
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
        shortcutName.TextChanged+=(_,_)=>UpdateShortcut();
        foreach(var check in checks){check.Checked+=(_,_)=>UpdateShortcut();check.Unchecked+=(_,_)=>UpdateShortcut();}
        mainKey.SelectionChanged+=(_,_)=>UpdateShortcut();
        record.Click+=(_,_)=>StartRecording();recordingTimeout.Tick+=(_,_)=>StopRecording();
        record.LostFocus+=(_,_)=>StopRecording();
        foreach(var button in new[]{trash,savePreset})
        {
            var background=button==trash?trash.Background:presetThemeBrush;button.Background=background;
            button.Resources["ButtonBackgroundDisabled"]=background;
            button.Resources["ButtonBorderBrushDisabled"]=button.BorderBrush??new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            button.Resources["ButtonForegroundDisabled"]=new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255,120,120,120));
        }
        trash.Click+=async(_,_)=>await RemoveButtonAsync();
        savePreset.Click+=async(_,_)=>await SaveSelectedPresetAsync();
        preview.ButtonSelected+=id=>{selectedButtonId=id;RefreshButton();buttonPageChanged?.Invoke(true);};
        preview.CenterSelected+=()=>{CancelDrag();selectedButtonId=RadialMenuView.CenterButtonId;RefreshButton();buttonPageChanged?.Invoke(true);};
        preview.ButtonPressed+=(id,e)=>BeginDrag(id,null,e);
        root.Background=new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);root.PointerPressed+=ClearSelectionOnBlank;root.PointerMoved+=DragMoved;root.PointerReleased+=DragReleased;root.PointerCaptureLost+=(_,_)=>CancelDrag();root.PointerCanceled+=(_,_)=>CancelDrag();
        KeyDown+=(_,e)=>{if(e.Key==global::Windows.System.VirtualKey.Escape){CancelDrag();e.Handled=true;}};
        Unloaded+=(_,_)=>{CancelDrag();StopRecording();};
        RefreshPresets();RefreshProfiles();UpdateDirty();Loaded+=(_,_)=>{AlignColumnContent(libraryCard);AlignColumnContent(right);};
    }
    private MenuEntry? Entry=>draft.Find(profileId).Entries.FirstOrDefault(e=>e.Id==selectedButtonId);
    private bool newMenuPending;
    private void QueueNewMenu()
    {
        if(newMenuPending||dialogOpen)return;newMenuPending=true;profiles.IsDropDownOpen=false;
        DispatcherQueue.TryEnqueue(async()=>
        {
            try
            {
                if(!await ConfirmPendingChangesAsync("新建菜单")){RestoreProfileSelection();return;}
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
            profiles.IsDropDownOpen=false;
            var ids=draft.Profiles.Select(p=>p.Id).ToHashSet();
            foreach(var item in profiles.Items.Cast<ComboBoxItem>().Where(i=>(string)i.Tag!="$new"&&!ids.Contains((string)i.Tag)).ToArray())
            {
                if(item.Content is ProfileChoice removed)profileRowTools.Remove(removed);
                profiles.Items.Remove(item);
            }
            foreach(var p in draft.Profiles)
            {
                var item=profiles.Items.Cast<ComboBoxItem>().FirstOrDefault(i=>(string)i.Tag==p.Id);
                if(item is null)
                {
                    item=new ComboBoxItem{Content=ProfileRow(p),ContentTemplate=profileRowTemplate,Tag=p.Id};
                    var create=profiles.Items.Cast<ComboBoxItem>().FirstOrDefault(i=>(string)i.Tag=="$new");
                    profiles.Items.Insert(create is null?profiles.Items.Count:profiles.Items.IndexOf(create),item);
                }
                else if(item.Content is ProfileChoice row){row.SetName(p.Name);row.SetDelete(new RowCommand(()=>QueueProfileOperation(p.Id,true),!p.IsDefault));}
                if(p.Id==profileId)profiles.SelectedItem=item;
            }
            if(!profiles.Items.Cast<ComboBoxItem>().Any(i=>(string)i.Tag=="$new"))
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
            menuEnabled.IsOn=p.Enabled;priority.Value=p.Priority;delete.IsEnabled=!p.IsDefault;menuSize.Value=p.SizeScale*100;buttonGap.Value=p.ButtonGap;
        }
        finally{loading=false;}
        RefreshCenterSettings();RefreshRings(0);RefreshButton();RefreshMatchWarning();BuildAppearanceSidebar();
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
            foreach(var control in new UIElement[]{buttonName,iconPicker,actionChoices})
                control.Visibility=centerSelected?Visibility.Collapsed:Visibility.Visible;
            if(centerSelected){trash.IsEnabled=savePreset.IsEnabled=false;StopRecording();shortcutPanel.Visibility=launchPanel.Visibility=Visibility.Collapsed;RefreshCenterSettings();RefreshPreview();return;}
            var e=Entry;if(e is null&&selectedButtonId is not null){selectedButtonId=draft.Find(profileId).Entries.FirstOrDefault()?.Id;e=Entry;}
            if(e is null)buttonPageChanged?.Invoke(false);actionChoices.Visibility=e is null?Visibility.Collapsed:Visibility.Visible;buttonName.IsEnabled=iconPicker.IsEnabled=trash.IsEnabled=savePreset.IsEnabled=e is not null;
            foreach(var choice in actionChoices.Children.OfType<Button>())
                choice.IsEnabled=e is not null;
            buttonName.Text=e?.Label??"";
            var icon=new StackPanel {Orientation=Orientation.Horizontal,Spacing=8,HorizontalAlignment=HorizontalAlignment.Left};icon.Children.Add(ButtonIcons.Create(e?.Glyph??actions.FirstOrDefault(a=>a.Id==e?.ActionId)?.Glyph,e?.Image));icon.Children.Add(new TextBlock{Text="选择图标 / 图片",VerticalAlignment=VerticalAlignment.Center});iconPicker.Content=icon;

            RefreshActionSummary();RefreshLaunchFields();
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