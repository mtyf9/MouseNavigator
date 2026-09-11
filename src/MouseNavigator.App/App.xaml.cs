using Microsoft.UI.Xaml;
namespace MouseNavigator.App;
public partial class App : Application
{
    private MainWindow? window;
    private Mutex? instance;
    public App()
    {
#if DEBUG
        var arguments = Environment.GetCommandLineArgs();
        var smoke = Array.IndexOf(arguments, "--smoke-test");
        if (smoke >= 0 && smoke + 1 < arguments.Length)
        {
            var directory = arguments[smoke + 1];
            UnhandledException += (_, e) =>
            {
                try
                {
                    Directory.CreateDirectory(directory);
                    File.AppendAllText(Path.Combine(directory, "unhandled.txt"), e.Exception + Environment.NewLine);
                }
                catch { /* Diagnostics must not replace the original failure. */ }
            };
        }
#endif
        InitializeComponent();
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var mutexName = @"Local\MouseNavigator.WinUI3";
#if DEBUG
        var arguments = Environment.GetCommandLineArgs();
        var smoke = Array.IndexOf(arguments, "--smoke-test");
        if (smoke >= 0 && smoke + 1 < arguments.Length) mutexName += ".Smoke." + Environment.ProcessId;
#endif
        instance = new Mutex(true, mutexName, out var first);
        if (!first) { if(!Environment.GetCommandLineArgs().Contains("--startup"))MouseNavigator.Windows.TrayIcon.RequestShow();instance.Dispose(); Exit(); return; }
        window = new MainWindow();
        window.Closed += (_, _) => { instance.ReleaseMutex(); instance.Dispose(); };
        if(Environment.GetCommandLineArgs().Contains("--startup")&&window.CanHideToTray)window.AppWindow.Hide();
        else window.Activate();
#if DEBUG
        if (smoke >= 0 && smoke + 1 < arguments.Length)
        {
            window.PauseForSmoke();
            window.DispatcherQueue.TryEnqueue(async () => await SmokeScenario.RunAsync(window, arguments[smoke + 1]));
        }
#endif
    }
}
