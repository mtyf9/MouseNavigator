using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using MouseNavigator.Windows;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.FirstOrDefault() == "--fixture") return Fixture(int.Parse(args[1]));
        var original = Native.GetForegroundWindow();
        using var process = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, $"--fixture {Environment.ProcessId}") {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardInput = true
        })!;
        try
        {
            var line = process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
            var handles = JsonSerializer.Deserialize<long[]>(line!)!.Select(h => (nint)h).ToArray();
            using var catalog = new WindowCatalog();
            var all = catalog.Enumerate();
            Check(handles.Take(3).All(h => all.Any(w => w.Identity.Handle == h)), "Visible and minimized fixture windows are enumerated");
            Check(handles.Skip(3).All(h => all.All(w => w.Identity.Handle != h)), "Hidden and tool fixture windows are excluded");
            var filtered = new FixtureCatalog(catalog, process.Id);
            var baseline = filtered.Enumerate();
            var platform = new PlatformActions(filtered);
            var source = baseline[0].Identity.Handle;
            var visited = new HashSet<nint>();
            for (var step = 1; step <= 3; step++)
            {
                var result = platform.SwitchWindow(source, WindowDirection.Next);
                Check(result.Succeeded, $"Native next activation {step}");
                source = baseline[step % 3].Identity.Handle;
                WaitForeground(source);
                visited.Add(source);
            }
            Check(visited.Count == 3, "Three-window cycle visits every native window");
            Check(platform.SwitchWindow(source, WindowDirection.Previous).Succeeded, "Native previous activation");
            WaitForeground(baseline[2].Identity.Handle);
            Check(!Native.IsIconic(handles[2]), "Minimized window was restored");
            var shortcutTarget = handles[1];
            Check(platform.SendShortcut(shortcutTarget, 0x11, 0x10, 0x53).Succeeded, "Custom Ctrl+Shift+S sends to fixture window");
            var received = process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Check(received == "shortcut:Ctrl+Shift+S", "Fixture receives the actual shortcut chord");
            Check(platform.SendShortcut(shortcutTarget, 0x2E).Succeeded, "Delete key sends to fixture window");
            received = process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Check(received == "shortcut:Delete", "Fixture receives Delete");
            var releaseWait = Stopwatch.StartNew();
            while (new[] { 0x11, 0x10, 0x53, 0x2E }.Any(k => (Native.GetAsyncKeyState(k) & 0x8000) != 0) && releaseWait.ElapsedMilliseconds < 2000)
                Thread.Sleep(20);
            Check(new[] { 0x11, 0x10, 0x53, 0x2E }.All(k => (Native.GetAsyncKeyState(k) & 0x8000) == 0), "Injected shortcut keys are released");
            process.StandardInput.WriteLine("dialog");
            process.StandardInput.Flush();
            var dialogLine = process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            var dialog = (nint)long.Parse(dialogLine!);
            WaitForeground(dialog);
            Check(filtered.Enumerate().All(w => w.Identity.Handle != dialog), "Owned dialog is not a separate cycle entry");
            var ownerIndex = baseline.ToList().FindIndex(w => w.Identity.Handle == handles[0]);
            Check(platform.SwitchWindow(dialog, WindowDirection.Next).Succeeded, "Navigation from dialog resolves its owner");
            WaitForeground(baseline[(ownerIndex + 1) % baseline.Count].Identity.Handle);
            Console.WriteLine("Windows smoke: all checks passed");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            if (!process.HasExited) { process.Kill(); process.WaitForExit(); }
            if (original != 0) Native.SetForegroundWindow(original);
        }
    }
    static void WaitForeground(nint expected)
    {
        var timer = Stopwatch.StartNew();
        while (Native.GetForegroundWindow() != expected && timer.ElapsedMilliseconds < 2000) Thread.Sleep(20);
        Check(Native.GetForegroundWindow() == expected, $"Foreground handle matches requested target (expected {expected}, actual {Native.GetForegroundWindow()})");
    }
    static void Check(bool pass, string name) { if (!pass) throw new Exception("FAIL " + name); Console.WriteLine("PASS " + name); }
    static int Fixture(int parent)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        var forms = Enumerable.Range(0, 5).Select(i => new Form {
            Text = "MouseNavigator test " + (char)('A' + i), Width = 350, Height = 180,
            StartPosition = FormStartPosition.Manual, Left = 140 + i * 50, Top = 160 + i * 30,
            FormBorderStyle = i == 4 ? FormBorderStyle.FixedToolWindow : FormBorderStyle.Sizable,
            ShowInTaskbar = i != 4, KeyPreview = true
        }).ToArray();
        foreach (var form in forms) { _ = form.Handle; form.Controls.Add(new Label { Text = "Temporary native window-switch test", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleCenter }); }
        forms[1].KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.S && e.Control && e.Shift && !e.Alt)
                Console.WriteLine("shortcut:Ctrl+Shift+S");
            else if (e.KeyCode == Keys.Delete) Console.WriteLine("shortcut:Delete");
            else return;
            e.Handled = true;
            Console.Out.Flush();
        };
        for (var i = 0; i < forms.Length; i++) if (i != 3) forms[i].Show();
        forms[2].WindowState = FormWindowState.Minimized;
        Native.AllowSetForegroundWindow((uint)parent);
        Console.WriteLine(JsonSerializer.Serialize(forms.Select(f => f.Handle.ToInt64())));
        Console.Out.Flush();
        using var shutdown = new System.Windows.Forms.Timer { Interval = 25000 };
        shutdown.Tick += (_, _) => Application.Exit();
        shutdown.Start();
        _ = Task.Run(() =>
        {
            if (Console.ReadLine() == "dialog") forms[0].BeginInvoke(() =>
            {
                var dialog = new Form { Text = "MouseNavigator test owned dialog", Width = 240, Height = 140, ShowInTaskbar = false };
                dialog.Show(forms[0]);
                Native.AllowSetForegroundWindow((uint)parent);
                Console.WriteLine(dialog.Handle.ToInt64());
                Console.Out.Flush();
            });
        });
        Application.Run();
        return 0;
    }
    sealed class FixtureCatalog(WindowCatalog catalog, int pid) : IWindowCatalog
    {
        public MouseNavigator.Contracts.ApplicationContext Capture(nint hwnd) => catalog.Capture(hwnd);
        public IReadOnlyList<WindowCandidate> Enumerate() => catalog.Enumerate().Where(w => w.Identity.ProcessId == pid).ToArray();
    }
    static class Native
    {
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint hwnd);
        [DllImport("user32.dll")] internal static extern bool IsIconic(nint hwnd);
        [DllImport("user32.dll")] internal static extern bool AllowSetForegroundWindow(uint pid);
    }
}
