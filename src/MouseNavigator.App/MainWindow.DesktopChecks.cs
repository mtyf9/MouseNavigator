#if DEBUG
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using MouseNavigator.Core;
using MouseNavigator.Windows;
namespace MouseNavigator.App;
public sealed partial class MainWindow
{
    internal async Task CheckTrayHintAsync(List<string> results)
    {
        void Check(bool value,string text)=>results.Add((value?"PASS: ":"FAIL: ")+text);
        static IEnumerable<DependencyObject> Children(DependencyObject parent)
        {
            for(var i=0;i<Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);i++)
            {
                var child=Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent,i);
                yield return child;
                foreach(var descendant in Children(child))yield return descendant;
            }
        }
        async Task Answer(Microsoft.UI.Xaml.Controls.ContentDialog dialog,bool remember,string name)
        {
            ((Microsoft.UI.Xaml.Controls.StackPanel)dialog.Content).Children.OfType<Microsoft.UI.Xaml.Controls.CheckBox>().Single().IsChecked=remember;
            await Task.Delay(100);
            var button=Children(dialog).OfType<Microsoft.UI.Xaml.Controls.Button>().Single(b=>b.Name==name);
            var peer=new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(button);
            ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)peer.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();
        }
        TrayHintOpenedForSmoke=d=>Answer(d,true,"CloseButton");
        await HideToTrayAsync();
        Check(AppWindow.IsVisible&&!File.Exists(TrayHintPreferencePath),"Cancel keeps the window open and does not remember the checkbox");
        TrayHintOpenedForSmoke=d=>Answer(d,false,"PrimaryButton");
        await HideToTrayAsync();
        Check(!AppWindow.IsVisible&&!File.Exists(TrayHintPreferencePath),"Confirm without opting out hides to tray and keeps future reminders");
        ShowMainWindow();
        TrayHintOpenedForSmoke=d=>Answer(d,true,"PrimaryButton");
        await HideToTrayAsync();
        Check(!AppWindow.IsVisible&&File.ReadAllText(TrayHintPreferencePath)=="1","Confirm with opt-out persists the local preference");
        ShowMainWindow();
        var opened=false;TrayHintOpenedForSmoke=d=>{opened=true;return Answer(d,false,"CloseButton");};
        await HideToTrayAsync();
        Check(!opened&&!AppWindow.IsVisible,"Subsequent close reads the persisted preference and skips the dialog");
        TrayHintOpenedForSmoke=null;ShowMainWindow();
    }
    internal async Task CheckDesktopAsync(List<string> results,string directory)
    {
        void Check(bool condition,string text)=>results.Add((condition?"PASS: ":"FAIL: ")+text);
        if(Environment.GetCommandLineArgs().Contains("--startup"))
            Check(!AppWindow.IsVisible&&CanHideToTray,"Startup mode remains hidden with an active tray icon");
        ShowMainWindow();
        var original=editor.ConfigurationForBackup;
        ShowEditorForSmoke();await Task.Delay(150);editor.EditShortcutForSmoke();await Task.Delay(100);
        var backup=editor.ConfigurationForBackup;
        var path=Path.Combine(directory,"backup.json");File.WriteAllText(path,ConfigurationCodec.Serialize(backup));
        Check(editor.IsDirty&&ConfigurationStore.Read(path).Shortcuts.Any(s=>s.Name=="保存文件"),
            "Backup includes unsaved shortcut edits without applying them");
        await ApplyRestoredConfigurationAsync(ConfigurationStore.Read(path));await Task.Delay(150);
        Check(!editor.IsDirty&&ConfigurationCodec.Serialize(ConfigurationStore.Read(store.FilePath))==ConfigurationCodec.Serialize(backup),
            "Restore writes the full configuration and resets the editor saved baseline");
        var invalid=backup with{Profiles=[]};var rejected=false;
        try{await ApplyRestoredConfigurationAsync(invalid);}catch(ArgumentException){rejected=true;}
        Check(rejected&&ConfigurationCodec.Serialize(ConfigurationStore.Read(store.FilePath))==ConfigurationCodec.Serialize(backup),
            "Invalid restore leaves saved configuration unchanged");

        var testKey=@"Software\MouseNavigator\Smoke\"+Guid.NewGuid().ToString("N");
        var actualStartup=new StartupRegistration(Environment.ProcessPath!);
        var priorStartup=actualStartup.Enabled;
        try
        {
            var registration=new StartupRegistration(@"C:\Program Files\MouseNavigator\MouseNavigator.App.exe",testKey);
            registration.SetEnabled(true);
            Check(registration.Enabled&&registration.Command=="\"C:\\Program Files\\MouseNavigator\\MouseNavigator.App.exe\" --startup",
                "Startup registration quotes executable paths and includes background startup");
            registration.SetEnabled(false);
            Check(!registration.Enabled&&actualStartup.Enabled==priorStartup,"Disabling the isolated startup item works without changing real Windows startup settings");
        }
        finally{Registry.CurrentUser.DeleteSubKeyTree(testKey,throwOnMissingSubKey:false);}
        Check(CanHideToTray&&File.Exists(Path.Combine(AppContext.BaseDirectory,"Assets","MouseNavigator.ico")),
            "The native notification icon registers successfully using the app icon");
        var hwnd=WinRT.Interop.WindowNative.GetWindowHandle(this);
        File.WriteAllText(TrayHintPreferencePath,"1");
        PostMessageW(hwnd,0x10,0,0);await Task.Delay(180);
        Check(!AppWindow.IsVisible&&CanHideToTray,"Closing the main window hides it while the tray remains registered");
        PostMessageW(hwnd,RegisterWindowMessageW("MouseNavigator.ShowMainWindow"),0,0);await Task.Delay(180);
        Check(AppWindow.IsVisible,"The native tray/second-launch show message reopens the main window");
        var enabled=EnabledSwitch.IsOn;HandleTrayCommand(TrayCommand.Toggle);
        Check(EnabledSwitch.IsOn!=enabled,"Tray pause toggles the same switch as the home page");
        HandleTrayCommand(TrayCommand.Toggle);PauseForSmoke();
        await ApplyRestoredConfigurationAsync(original);
        Navigation.SelectedItem=Navigation.MenuItems.Cast<Microsoft.UI.Xaml.Controls.NavigationViewItem>().First();
        await Task.Delay(150);
        await SmokeScenario.RenderAsync((FrameworkElement)Content,Path.Combine(directory,"desktop-home.png"));
        tray?.Dispose();
        Check(!CanHideToTray,"Disposing removes the notification icon");
    }
    [DllImport("user32.dll")] private static extern bool PostMessageW(nint window,uint message,nuint wParam,nint lParam);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern uint RegisterWindowMessageW(string message);
}
#endif