using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MouseNavigator.Actions.BuiltIn;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using MouseNavigator.Windows;
using Windows.Storage;
using Windows.Storage.Pickers;
namespace MouseNavigator.App;

public sealed partial class MainWindow : Window
{
    private readonly WindowCatalog catalog;
    private readonly PlatformActions platform;
    private readonly ConfigurationStore store;
    private readonly MenuEditor editor;
    private ActionRegistry registry;
    private NavigationController? controller;
    private bool closeApproved, closeDialogOpen;
    // These state controls are not mounted in the window; the tray owns their commands.
    private readonly ToggleSwitch EnabledSwitch=new(){IsOn=true},StartupSwitch=new();
    private readonly TextBlock StartupHint=new(),ContextText=new();
    private InfoBar StatusBar=>editor.NotificationBar;
    private readonly Button BackupButton=new(),RestoreButton=new();

    public MainWindow()
    {
        InitializeComponent();
        EnabledSwitch.Toggled+=EnabledSwitch_Toggled;StartupSwitch.Toggled+=StartupSwitch_Toggled;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);
        SystemBackdrop = new MicaBackdrop();
        var scale = OverlayWindow.WindowScale(WinRT.Interop.WindowNative.GetWindowHandle(this));
        var area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Min((int)(1480 * scale), area.Width - (int)(32 * scale));
        var height = Math.Min((int)(980 * scale), area.Height - (int)(32 * scale));
        AppWindow.MoveAndResize(new(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height));
        catalog = new WindowCatalog();
        platform = new PlatformActions(catalog);
        store = CreateStore();
        var loaded = store.Load(DefaultProfiles.Configuration);
        registry = CreateRegistry(loaded.Configuration);
        var resolver = new ProfileResolver(loaded.Configuration.Profiles);
        editor = new MenuEditor(loaded.Configuration, registry.Actions
            .Where(a => !a.Descriptor.Id.StartsWith("shortcuts.", StringComparison.Ordinal)).Select(a => a.Descriptor).ToArray())
        {
            OpenSettingsRequested=ShowSettingsWindow,
            OwnerWindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this),
            SaveRequested = SaveConfigurationAsync,
            PickImageRequested = PickIconAsync,
            PickProgramsRequested = PickProgramsAsync,
            PickApplicationRequested = PickApplicationAsync,
            ImportMenuRequested = ImportMenuAsync,
            ExportMenuRequested = ExportMenuAsync
        };
        ProfilesPage.Children.Add(editor);
        AppWindow.Changed+=(_,_)=>editor.RefreshDisplaySize();

        if (loaded.Warning is not null)
        {

            editor.Notify(loaded.Warning,InfoBarSeverity.Warning,"配置读取提示");
        }
        try
        {
            controller = new NavigationController(DispatcherQueue, catalog, registry, resolver, platform.ActivateWindow);
            controller.ConfigureTrigger(loaded.Configuration.Trigger??new());
            controller.ContextChanged += text => ContextText.Text = text;
            controller.Completed += result =>
            {
                if(!result.Succeeded)editor.Notify(result.Message,InfoBarSeverity.Warning,"未执行");
            };
        }
        catch (Exception ex)
        {
            EnabledSwitch.IsOn = false;
            EnabledSwitch.IsEnabled = false;
            editor.Notify(ex.Message,InfoBarSeverity.Error,"鼠标监听启动失败");
        }
        AppWindow.Closing += (sender, args) =>
        {
            if(closeApproved)return;
            args.Cancel=true;
            if(homeOperation||closeDialogOpen||!editor.IsEnabled||editor.HasOpenDialog)return;
            if(CanHideToTray)_=HideToTrayAsync();
            else _=RequestExitAsync();
        };
        Activated+=(_,args)=>{if(args.WindowActivationState==WindowActivationState.Deactivated)editor.StopRecording();else RefreshStartupState();};
        Closed += (_, _) => { settingsWindow?.Close();tray?.Dispose();editor.StopRecording();controller?.Dispose(); catalog.Dispose(); };
        InitializeDesktopIntegration();
    }

    private static ConfigurationStore CreateStore()
    {
#if DEBUG
        var arguments = Environment.GetCommandLineArgs();
        var smoke = Array.IndexOf(arguments, "--smoke-test");
        if (smoke >= 0 && smoke + 1 < arguments.Length)
            return new(Path.Combine(arguments[smoke + 1], "settings.json"));
#endif
        return new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MouseNavigator", "settings.json"));
    }
    private ActionRegistry CreateRegistry(NavigatorConfiguration configuration)
    {
        ConfigurationCodec.Validate(configuration);
        var result = new ActionRegistry();
        result.Register(new WindowsPlugin(), platform);
        result.Register(new ConfiguredShortcutsPlugin(configuration.Shortcuts), platform);
        return result;
    }
    private async Task SaveConfigurationAsync(NavigatorConfiguration configuration)
    {
        var updatedRegistry = CreateRegistry(configuration);
        var updatedResolver = new ProfileResolver(configuration.Profiles);
        await Task.Run(() => store.Save(configuration));
        controller?.ApplyConfiguration(updatedRegistry, updatedResolver);
        controller?.ConfigureTrigger(configuration.Trigger??new());
        registry = updatedRegistry;

        ContextText.Text = "配置已更新，等待下一次导航";
    }
    private async Task<NavigatorConfiguration?> ImportConfigurationAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".json");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        var file = await picker.PickSingleFileAsync();
        if (file is null) return null;
        return await Task.Run(() => ConfigurationStore.Read(file.Path));
    }
    private async Task ExportConfigurationAsync(NavigatorConfiguration configuration)
    {
        var json = ConfigurationCodec.Serialize(configuration);
        var picker = new FileSavePicker { SuggestedFileName = "MouseNavigator-backup-"+DateTime.Now.ToString("yyyyMMdd-HHmm") };
        picker.FileTypeChoices.Add("配置备份", new List<string> { ".json" });
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;
        await FileIO.WriteTextAsync(file, json);
        ShowHomeStatus("备份完成","全部菜单和预设已保存到备份文件。",InfoBarSeverity.Success);
    }
    private async Task<string?> PickApplicationAsync()
    {
        var picker=new FileOpenPicker();picker.FileTypeFilter.Add(".exe");
        WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));
        return (await picker.PickSingleFileAsync())?.Path;
    }
    private async Task<IReadOnlyList<string>> PickProgramsAsync()
    {
        var picker=new FileOpenPicker();picker.FileTypeFilter.Add(".exe");
        WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));
        var files=await picker.PickMultipleFilesAsync();return files.Select(f=>f.Name).ToArray();
    }
    private async Task<MenuDocument?> ImportMenuAsync()
    {
        var picker=new FileOpenPicker();picker.FileTypeFilter.Add(".json");
        WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));
        var file=await picker.PickSingleFileAsync();if(file is null)return null;
        if((await file.GetBasicPropertiesAsync()).Size>ConfigurationCodec.MaximumBytes)throw new ArgumentException("菜单文件不能超过 16 MB。");
        return MenuDocumentCodec.Deserialize(await FileIO.ReadTextAsync(file));
    }
    private async Task ExportMenuAsync(MenuDocument document)
    {
        var json=MenuDocumentCodec.Serialize(document);
        var safeName=string.Concat(document.Menu.Name.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c));
        var picker=new FileSavePicker{SuggestedFileName=safeName};picker.FileTypeChoices.Add("悬浮菜单",new List<string>{".json"});
        WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));
        var file=await picker.PickSaveFileAsync();if(file is not null)await FileIO.WriteTextAsync(file,json);
    }
    private async Task<string?> PickIconAsync()
    {
        var picker=new FileOpenPicker();
        foreach(var ext in new[]{".png",".jpg",".jpeg",".bmp",".ico"})picker.FileTypeFilter.Add(ext);
        WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));
        var file=await picker.PickSingleFileAsync();
        return file is null?null:await ButtonIcons.ImportAsync(file);
    }
#if DEBUG
    internal void PauseForSmoke() { if (controller is not null) controller.Enabled = false; }
    internal MenuEditor EditorForSmoke => editor;
    internal void ShowEditorForSmoke()
    {
        ProfilesPage.Visibility=Visibility.Visible;
    }
    internal void ScrollEditorForSmoke() => PageScroll.ChangeView(null, PageScroll.ScrollableHeight, null);
    internal void CloseForSmoke() { closeApproved = true; Close(); }
#endif
    private void EnabledSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (controller is not null) controller.Enabled = EnabledSwitch.IsOn;
        tray?.Update(EnabledSwitch.IsOn,EnabledSwitch.IsEnabled&&controller is not null);
    }
}
