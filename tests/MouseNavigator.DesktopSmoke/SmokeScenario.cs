// Linked only into Debug builds. Renders the real WinUI visual tree for development validation.
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using MouseNavigator.Actions.BuiltIn;
using MouseNavigator.Windows;
using Windows.Graphics.Imaging;
using Windows.Storage;
using System.Runtime.InteropServices.WindowsRuntime;

namespace MouseNavigator.App;
internal static class SmokeScenario
{
    public static async Task RunAsync(MainWindow main, string directory)
    {
        Directory.CreateDirectory(directory);
        try
        {
            if(Environment.GetCommandLineArgs().Contains("--dpi-check"))
            {
                await Task.Delay(350);var focusedResults=new List<string>();
                await main.CheckDisplaysAsync(focusedResults,directory);
                await File.WriteAllLinesAsync(Path.Combine(directory,"result.txt"),focusedResults);return;
            }
            if(Environment.GetCommandLineArgs().Contains("--tray-hint-check"))
            {
                await Task.Delay(350);var focusedResults=new List<string>();
                await main.CheckTrayHintAsync(focusedResults);
                await File.WriteAllLinesAsync(Path.Combine(directory,"result.txt"),focusedResults);return;
            }
            if(Environment.GetCommandLineArgs().Contains("--desktop-check"))
            {
                await Task.Delay(350);var focusedResults=new List<string>();
                await main.CheckDesktopAsync(focusedResults,directory);
                await File.WriteAllLinesAsync(Path.Combine(directory,"result.txt"),focusedResults);return;
            }
            if(Environment.GetCommandLineArgs().Contains("--inner-ring-check"))
            {
                await Task.Delay(350);main.ShowEditorForSmoke();var focusedResults=new List<string>();
                await main.EditorForSmoke.CheckInnerRingAsync(focusedResults,directory);
                await File.WriteAllLinesAsync(Path.Combine(directory,"result.txt"),focusedResults);return;
            }
            if(Environment.GetCommandLineArgs().Contains("--layout-check"))
            {
                await Task.Delay(350);main.ShowEditorForSmoke();var focusedResults=new List<string>();
                await main.EditorForSmoke.CheckLayoutAsync(focusedResults,directory);
                await File.WriteAllLinesAsync(Path.Combine(directory,"result.txt"),focusedResults);return;
            }
            if(Environment.GetCommandLineArgs().Contains("--management-check"))
            {
                await Task.Delay(350);main.ShowEditorForSmoke();var focusedResults=new List<string>();
                await main.EditorForSmoke.CheckManagementAsync(focusedResults,directory);
                await File.WriteAllLinesAsync(Path.Combine(directory,"result.txt"),focusedResults);return;
            }
            if(Environment.GetCommandLineArgs().Contains("--center-check"))
            {
                await Task.Delay(350);main.ShowEditorForSmoke();var focusedResults=new List<string>();
                await main.EditorForSmoke.CheckCenterOptionsAsync(focusedResults,directory);
                await File.WriteAllLinesAsync(Path.Combine(directory,"result.txt"),focusedResults);return;
            }
            if(Environment.GetCommandLineArgs().Contains("--editor-check"))
            {
                await Task.Delay(350);main.ShowEditorForSmoke();var focusedResults=new List<string>();
                await main.EditorForSmoke.CheckStationaryDragAndCenterAsync(focusedResults,directory);
                await File.WriteAllLinesAsync(Path.Combine(directory,"result.txt"),focusedResults);return;
            }
            await Task.Delay(700);
            await RenderAsync((FrameworkElement)main.Content, Path.Combine(directory, "home.png"));
            using var catalog = new WindowCatalog();
            var registry = new ActionRegistry();
            registry.Register(new WindowsPlugin(), new PlatformActions(catalog));
            var ring = new RingWindow();
            var before = WindowCatalog.Foreground;
            ring.SetMenu(DefaultProfiles.Create()[0], registry);
            ring.ShowAt(600, 420);
            ring.Highlight("right");
            await Task.Delay(400);
            var noActivate = WindowCatalog.Foreground == before;
            await RenderAsync((FrameworkElement)ring.Content, Path.Combine(directory, "ring.png"));
            ring.HideRing();
            ring.Close();
            var results = new List<string> { noActivate ? "PASS: WinUI rendered; overlay did not activate." : "FAIL: overlay changed foreground." };
            await CheckMiddleGesturesAsync(main, results);
            await CheckWindowPreviewAsync(main, directory, results);
            await CheckDynamicRingAsync(main, results);
            main.ShowEditorForSmoke();
            await main.EditorForSmoke.CheckOpenSelectionsForSmokeAsync(results);
            await main.EditorForSmoke.CheckDragReorderForSmokeAsync(results,directory);
            await main.EditorForSmoke.CheckButtonEditingForSmokeAsync(results);
            await main.EditorForSmoke.CheckDialogsForSmokeAsync(results,directory);
            main.EditorForSmoke.EditShortcutForSmoke();
            await Task.Delay(300);
            await RenderAsync((FrameworkElement)main.Content, Path.Combine(directory, "editor.png"));
            var edited = main.EditorForSmoke.DraftForSmoke;
            var sharedRegistry = new ActionRegistry();
            sharedRegistry.Register(new WindowsPlugin(), new RecordingPlatform());
            sharedRegistry.Register(new ConfiguredShortcutsPlugin(edited.Shortcuts), new RecordingPlatform());
            var actualRing = new RingWindow();
            try
            {
                var editedProfile = edited.Profiles.Single(p => p.IsDefault);
                actualRing.SetMenu(editedProfile, sharedRegistry);
                actualRing.ShowAt(650, 450);
                actualRing.Highlight(main.EditorForSmoke.SelectedButtonForSmoke);
                await Task.Delay(150);
                results.Add(actualRing.ViewForSmoke.Buttons.SequenceEqual(main.EditorForSmoke.PreviewForSmoke.Buttons)
                    && actualRing.ViewForSmoke.Diameter == main.EditorForSmoke.PreviewForSmoke.Diameter
                    ? "PASS: Editor and runtime share identical eight-button visuals and dimensions" : "FAIL: Editor and runtime visual mismatch");
                results.Add(actualRing.ViewForSmoke.CenterTextForSmoke == main.EditorForSmoke.PreviewForSmoke.CenterTextForSmoke
                    && actualRing.ViewForSmoke.HasCenterImageForSmoke && main.EditorForSmoke.PreviewForSmoke.HasCenterImageForSmoke
                    ? "PASS: Runtime and editor display the same custom center text and image" : "FAIL: Custom center visual mismatch");
                await RenderAsync(actualRing.ViewForSmoke, Path.Combine(directory, "ring-eight.png"));
                await RenderAsync(main.EditorForSmoke.PreviewForSmoke, Path.Combine(directory, "editor-ring-eight.png"));
            }
            finally { actualRing.HideRing(); actualRing.Close(); }
            var rightEntry = edited.Profiles.Single(p => p.IsDefault).Entries.Single(e => e.Id == "right");
            var shortcut = edited.Shortcuts.Single(s => s.Id == rightEntry.ActionId);
            results.Add(shortcut.Name == "保存文件" && shortcut.Keys.SequenceEqual(new ushort[] { 0x11, 0x53 })
                ? "PASS: Visual controls edit the right slot to Ctrl + S" : "FAIL: Visual shortcut editor did not update draft");
            main.ScrollEditorForSmoke();
            await Task.Delay(200);
            await RenderAsync((FrameworkElement)main.Content, Path.Combine(directory, "editor-files.png"));
            await main.EditorForSmoke.SaveDraftAsync();
            var reloaded = ConfigurationStore.Read(Path.Combine(directory, "settings.json"));
            results.Add(ConfigurationCodec.Serialize(reloaded) == ConfigurationCodec.Serialize(edited) && !main.EditorForSmoke.IsDirty
                ? "PASS: Editor saves and reloads the complete configuration" : "FAIL: Editor configuration round trip");
            await File.WriteAllLinesAsync(Path.Combine(directory, "result.txt"), results);
        }
        catch (Exception ex) { await File.WriteAllTextAsync(Path.Combine(directory, "result.txt"), "FAIL: " + ex); }
        finally { main.CloseForSmoke(); }
    }
    private static async Task CheckWindowPreviewAsync(MainWindow main, string directory, List<string> results)
    {
        var sources = new List<Window>();
        try
        {
            var candidates = new List<WindowCandidate>();
            for (var i = 0; i < 3; i++)
            {
                var color = global::Windows.UI.Color.FromArgb(255, (byte)(32 + i * 45), (byte)(70 + i * 22), (byte)(130 + i * 25));
                var grid = new Microsoft.UI.Xaml.Controls.Grid { Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(color) };
                grid.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock {
                    Text = $"预览测试窗口 {i + 1}\n保持中键 · 移动选择 · 松开切换", FontSize = 26, TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(24), VerticalAlignment = VerticalAlignment.Center });
                var window = new Window { Title = "Preview fixture " + (i + 1), Content = grid };
                sources.Add(window);
                var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                OverlayWindow.Configure(handle);
                OverlayWindow.ShowPanel(handle, new(80 + i * 60, 140 + i * 40, 440, 280, 1));
                candidates.Add(new(new(handle, (uint)Environment.ProcessId), window.Title));
            }
            await Task.Delay(300);
            var catalog = new PreviewFixtureCatalog(candidates);
            var registry = new ActionRegistry();
            registry.Register(new WindowsPlugin(), new RecordingPlatform());
            var activations = new List<WindowIdentity>();
            using var controller = new NavigationController(main.DispatcherQueue, catalog, registry,
                new ProfileResolver(DefaultProfiles.Create()), identity => { activations.Add(identity); return ActionResult.Success("Recorded activation"); });
            controller.Completed += result => { if (!result.Succeeded) results.Add("FAIL: Preview controller: " + result.Message); };
            var x = main.AppWindow.Position.X + main.AppWindow.Size.Width / 2;
            var y = main.AppWindow.Position.Y + main.AppWindow.Size.Height / 2;
            var up = y - (int)(90 * OverlayWindow.ScaleAt(x, y));
            void Check(bool value, string text) => results.Add((value ? "PASS: " : "FAIL: ") + text);
            async Task Open()
            {
                controller.QueueForSmoke(new MouseSample(MousePhase.Down, x, y, candidates[0].Identity.Handle),
                    new MouseSample(MousePhase.Move, x, up, 0));
                await Task.Delay(180);
            }
            var before = WindowCatalog.Foreground;
            await Open();
            var preview = controller.PreviewForSmoke!;
            Check(preview.IsOpen && !controller.RingVisibleForSmoke, "Entering preview opens the panel while middle remains held");
            Check(WindowCatalog.Foreground == before, "Window preview does not steal foreground");
            Check(preview.ThumbnailCountForSmoke == 3, "DWM registers and positions three live window thumbnails");
            await Task.Delay(700);
            Check(preview.IsOpen, "Window preview remains visible during a stationary hold");
            controller.QueueForSmoke(new MouseSample(MousePhase.Up, x, up, 0));
            await Task.Delay(80);
            Check(!preview.IsOpen && activations.Count == 0, "Immediate release after opening does not select an accidental target");

            await Open();
            var target = preview.CardPointForSmoke(1);
            controller.QueueForSmoke(new MouseSample(MousePhase.Move, target.X, target.Y, 0));
            await Task.Delay(120);
            Check(activations.Count == 0 && preview.SelectionAt(target.X, target.Y)?.Identity == candidates[1].Identity,
                "Moving over a thumbnail highlights without activating");
            await RenderAsync((FrameworkElement)preview.Content, Path.Combine(directory, "window-preview.png"));
            controller.QueueForSmoke(new MouseSample(MousePhase.Up, target.X, target.Y, 0), new MouseSample(MousePhase.Up, target.X, target.Y, 0));
            await Task.Delay(80);
            Check(activations.Count == 1 && activations[0] == candidates[1].Identity && !preview.IsOpen && preview.ThumbnailCountForSmoke == 0,
                $"Release activates exactly the selected window once and releases thumbnails (activations={activations.Count}, open={preview.IsOpen}, thumbnails={preview.ThumbnailCountForSmoke})");

            await Open();
            target = preview.CardPointForSmoke(1);
            controller.QueueForSmoke(new MouseSample(MousePhase.Move, target.X, target.Y, 0));
            await Task.Delay(80);
            catalog.Current = [candidates[0], candidates[2]];
            controller.QueueForSmoke(new MouseSample(MousePhase.Up, target.X, target.Y, 0));
            await Task.Delay(80);
            Check(activations.Count == 1, "A window closed before release cannot be activated");
            catalog.Current = candidates;
            await Open();
            controller.QueueForSmoke(new MouseSample(MousePhase.Move, x + 5000, y + 5000, 0), new MouseSample(MousePhase.Up, x + 5000, y + 5000, 0));
            await Task.Delay(80);
            Check(!preview.IsOpen && activations.Count == 1, "Release outside preview cancels");
            await Open();
            controller.Enabled = false;
            Check(!preview.IsOpen && preview.ThumbnailCountForSmoke == 0, "Pausing closes preview and disposes native thumbnails");
            controller.Enabled = true;
            await Open();
            controller.QueueForSmoke(new MouseSample(MousePhase.Cancel, x, y, 0));
            await Task.Delay(80);
            Check(!preview.IsOpen && activations.Count == 1, "Explicit cancellation closes preview without switching");
            // Extra entries exercise paging even on the largest supported panel.
            catalog.Current = candidates.Concat(Enumerable.Range(1, 20).Select(i =>
                new WindowCandidate(new((nint)(-i), 0), "Unavailable fixture " + i))).ToArray();
            await Open();
            var footer = preview.NextPagePointForSmoke;
            controller.QueueForSmoke(new MouseSample(MousePhase.Move, footer.X, footer.Y, 0));
            await Task.Delay(850);
            Check(preview.PageForSmoke == 1 && activations.Count == 1, "Holding over the footer advances one page without activating a window");
            controller.ApplyConfiguration(registry, new ProfileResolver(DefaultProfiles.Create()));
            Check(!preview.IsOpen && preview.ThumbnailCountForSmoke == 0, "Configuration changes cancel preview and dispose thumbnails");
            catalog.Current = [];
            await Open();
            Check(preview.IsOpen && preview.ThumbnailCountForSmoke == 0, "An empty desktop displays a safe empty preview");
            controller.QueueForSmoke(new MouseSample(MousePhase.Up, x, up, 0));
            await Task.Delay(80);
            Check(!preview.IsOpen && activations.Count == 1, "Empty preview release cancels without activation");
        }
        finally { foreach (var source in sources) source.Close(); }
    }
    private sealed class PreviewFixtureCatalog(IReadOnlyList<WindowCandidate> windows) : IWindowCatalog
    {
        public IReadOnlyList<WindowCandidate> Current { get; set; } = windows;
        public IReadOnlyList<WindowCandidate> Enumerate() => Current;
        public MouseNavigator.Contracts.ApplicationContext Capture(nint hwnd) => new(hwnd, "preview-fixture", "Preview fixture");
    }
    private static async Task CheckMiddleGesturesAsync(MainWindow main, List<string> results)
    {
        using var catalog = new WindowCatalog();
        var platform = new RecordingPlatform();
        var registry = new ActionRegistry();
        registry.Register(new WindowsPlugin(), platform);
        using var controller = new NavigationController(main.DispatcherQueue, catalog, registry, new ProfileResolver(DefaultProfiles.Create()));
        var errors = new List<string>();
        controller.Completed += result => { if (!result.Succeeded) errors.Add(result.Message); };
        var x = main.AppWindow.Position.X + main.AppWindow.Size.Width / 2;
        var y = main.AppWindow.Position.Y + main.AppWindow.Size.Height / 2;
        var scale = OverlayWindow.ScaleAt(x, y);
        var drag = (int)Math.Ceiling(20 * scale);
        var right = (int)Math.Round(100 * scale);
        var outside = (int)Math.Round(220 * scale);
        // A zero source handle prevents ordinary-click replay into unrelated applications.
        MouseSample Sample(MousePhase phase, int dx = 0) => new(phase, x + dx, y, 0);
        void Check(bool condition, string name) => results.Add((condition ? "PASS: " : "FAIL: ") + name);
        async Task Reset()
        {
            controller.QueueForSmoke(Sample(MousePhase.Cancel));
            await Task.Delay(100);
        }

        controller.QueueForSmoke(Sample(MousePhase.Down));
        await Task.Delay(800);
        Check(controller.GestureActiveForSmoke && !controller.RingVisibleForSmoke, "Stationary hold survives beyond the former 320 ms timeout");
        controller.QueueForSmoke(Sample(MousePhase.Move, drag));
        await Task.Delay(100);
        Check(controller.RingVisibleForSmoke, "First drag after a long hold opens the ring");
        await Task.Delay(800);
        Check(controller.RingVisibleForSmoke, "Visible ring survives a held button without further movement");
        await Reset();

        controller.QueueForSmoke(Sample(MousePhase.Down), Sample(MousePhase.Move, drag), Sample(MousePhase.Move));
        await Task.Delay(100);
        Check(controller.RingVisibleForSmoke, "Queued outward-and-back movement retains the threshold crossing");
        controller.QueueForSmoke(Sample(MousePhase.Up));
        await Task.Delay(100);
        Check(!controller.GestureActiveForSmoke && !controller.RingVisibleForSmoke && platform.Calls == 0, "Releasing in the center cancels without executing");
        await Reset();

        controller.QueueForSmoke(Sample(MousePhase.Down), Sample(MousePhase.Move, drag));
        await Task.Delay(100);
        controller.QueueForSmoke(Sample(MousePhase.Move, outside));
        await Task.Delay(800);
        Check(controller.RingVisibleForSmoke, "Dragging outside while held does not dismiss the ring");
        controller.QueueForSmoke(Sample(MousePhase.Up, outside));
        await Task.Delay(100);
        Check(!controller.RingVisibleForSmoke && platform.Calls == 0, "Releasing outside cancels without executing");
        await Reset();

        var repeated = true;
        for (var i = 0; i < 8; i++)
        {
            controller.QueueForSmoke(Sample(MousePhase.Down), Sample(MousePhase.Move, right));
            await Task.Delay(100);
            repeated &= controller.RingVisibleForSmoke;
            controller.QueueForSmoke(Sample(MousePhase.Up, right));
            await Task.Delay(100);
            repeated &= !controller.GestureActiveForSmoke && !controller.RingVisibleForSmoke && platform.Calls == i + 1;
        }
        Check(repeated, "Eight successive gestures each open and execute exactly once on release");

        controller.QueueForSmoke(Sample(MousePhase.Down), Sample(MousePhase.Move, drag));
        await Task.Delay(100);
        controller.Enabled = false;
        controller.QueueForSmoke(Sample(MousePhase.Move, right), Sample(MousePhase.Up, right));
        await Task.Delay(100);
        Check(!controller.GestureActiveForSmoke && !controller.RingVisibleForSmoke && platform.Calls == 8, "Pausing closes the ring and ignores the pending release");
        controller.Enabled = true;
        controller.QueueForSmoke(Sample(MousePhase.Down), Sample(MousePhase.Move, drag), Sample(MousePhase.Cancel), Sample(MousePhase.Move, right), Sample(MousePhase.Up, right));
        await Task.Delay(100);
        Check(!controller.GestureActiveForSmoke && !controller.RingVisibleForSmoke && platform.Calls == 8, "Canceled gesture cannot reopen or execute from trailing events");
        var draft = new ConfigurationDraft(DefaultProfiles.Configuration());
        draft.SetShortcut("global", "right", "保存文件", new ushort[] { 0x11, 0x53 });
        var updated = draft.Snapshot();
        var updatedRegistry = new ActionRegistry();
        updatedRegistry.Register(new WindowsPlugin(), platform);
        updatedRegistry.Register(new ConfiguredShortcutsPlugin(updated.Shortcuts), platform);
        controller.QueueForSmoke(Sample(MousePhase.Down), Sample(MousePhase.Move, right));
        await Task.Delay(100);
        controller.ApplyConfiguration(updatedRegistry, new ProfileResolver(updated.Profiles));
        controller.QueueForSmoke(Sample(MousePhase.Up, right));
        await Task.Delay(100);
        Check(!controller.GestureActiveForSmoke && !controller.RingVisibleForSmoke && platform.Calls == 8 && platform.Shortcuts.Count == 0,
            "Applying a configuration cancels the old gesture without executing");
        controller.QueueForSmoke(Sample(MousePhase.Down), Sample(MousePhase.Move, right), Sample(MousePhase.Up, right));
        await Task.Delay(100);
        Check(platform.Calls == 8 && platform.Shortcuts.Count == 1 && platform.Shortcuts[0].SequenceEqual(new ushort[] { 0x11, 0x53 }),
            "Next gesture executes the newly configured Ctrl + S through the plugin");
        Check(errors.Count == 0, "Gesture scenarios complete without controller errors");
    }

    private static async Task CheckDynamicRingAsync(MainWindow main, List<string> results)
    {
        using var catalog = new WindowCatalog();
        var platform = new RecordingPlatform();
        var registry = new ActionRegistry();
        registry.Register(new WindowsPlugin(), platform);
        var shortcuts = Enumerable.Range(0, 8).Select(i => new ShortcutDefinition("shortcuts.test" + i, "Test " + i, new ushort[] { 17, (ushort)(49 + i) })).ToArray();
        registry.Register(new ConfiguredShortcutsPlugin(shortcuts), platform);
        var buttons = shortcuts.Select((shortcut, i) => new MenuEntry("button-" + i, shortcut.Id)).Reverse().ToArray();
        var profile = new MenuProfile(3, "global", "Eight", [], buttons);
        using var controller = new NavigationController(main.DispatcherQueue, catalog, registry, new ProfileResolver([profile]));
        var x = main.AppWindow.Position.X + main.AppWindow.Size.Width / 2;
        var y = main.AppWindow.Position.Y + main.AppWindow.Size.Height / 2;
        var scale = OverlayWindow.ScaleAt(x, y);
        for (var i = 0; i < buttons.Length; i++)
        {
            var angle = RingGeometry.Angle(i, buttons.Length) * Math.PI / 180;
            var px = x + (int)(100 * scale * Math.Cos(angle));
            var py = y + (int)(100 * scale * Math.Sin(angle));
            controller.QueueForSmoke(new MouseSample(MousePhase.Down, x, y, 0), new MouseSample(MousePhase.Move, px, py, 0), new MouseSample(MousePhase.Up, px, py, 0));
            await Task.Delay(80);
        }
        results.Add(platform.Shortcuts.Count == 8 && platform.Shortcuts.Select(k => k[1]).SequenceEqual(shortcuts.Reverse().Select(s => s.Keys[1]))
            ? "PASS: All eight reordered runtime sectors execute their own action on middle release" : "FAIL: Dynamic sectors did not execute the expected action");
        var layered=profile with {RingCount=2,Entries=[new("preview","windows.window.preview"),new("outer","windows.maximize",Ring:1)]};
        controller.ApplyConfiguration(registry,new ProfileResolver([layered]));
        controller.QueueForSmoke(new MouseSample(MousePhase.Down,x,y,0),new MouseSample(MousePhase.Move,x,y-(int)(100*scale),0),new MouseSample(MousePhase.Move,x,y-(int)(210*scale),0),new MouseSample(MousePhase.Up,x,y-(int)(210*scale),0));
        await Task.Delay(100);
        results.Add(platform.Calls==1&&controller.PreviewForSmoke?.IsOpen!=true?"PASS: Passing through inner preview reaches and executes outer-ring button":"FAIL: Inner preview blocked outer-ring action");
        controller.QueueForSmoke(new MouseSample(MousePhase.Down,x,y,0),new MouseSample(MousePhase.Move,x,y-(int)(100*scale),0));
        await Task.Delay(450);
        results.Add(controller.PreviewForSmoke?.IsOpen==true?"PASS: Intentional hold opens preview in a multiring menu":"FAIL: Multiring preview dwell did not open");
        controller.QueueForSmoke(new MouseSample(MousePhase.Cancel,x,y,0));await Task.Delay(80);
        var empty = profile with { Entries = [new("blank", "")] };
        var errors = 0; controller.Completed += r => { if (!r.Succeeded) errors++; };
        controller.ApplyConfiguration(registry, new ProfileResolver([empty]));
        controller.QueueForSmoke(new MouseSample(MousePhase.Down, x, y, 0), new MouseSample(MousePhase.Move, x, y - (int)(100 * scale), 0), new MouseSample(MousePhase.Up, x, y - (int)(100 * scale), 0));
        await Task.Delay(80);
        results.Add(platform.Shortcuts.Count == 8 && errors == 0 ? "PASS: Unconfigured runtime button cancels without action errors" : "FAIL: Blank button executed");
    }
    private sealed class RecordingPlatform : IPlatformActions
    {
        public int Calls { get; private set; }
        public List<ushort[]> Shortcuts { get; } = [];
        public ActionResult SendShortcut(nint source, params ushort[] keys)
        {
            if (keys.SequenceEqual(new ushort[] { 0x5B, 0x26 })) Calls++;
            else Shortcuts.Add(keys.ToArray());
            return ActionResult.Success("Recorded shortcut");
        }
    }
    internal static async Task RenderAsync(FrameworkElement element, string path)
    {
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(element);
        var bytes = (await bitmap.GetPixelsAsync()).ToArray();
        var file = await StorageFile.GetFileFromPathAsync(CreateEmptyFile(path));
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, bytes);
        await encoder.FlushAsync();
    }
    private static string CreateEmptyFile(string path) { File.WriteAllBytes(path, []); return path; }
}
