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
            await Task.Delay(700);
            await RenderAsync((FrameworkElement)main.Content, Path.Combine(directory, "home.png"));
            using var catalog = new WindowCatalog();
            var registry = new ActionRegistry();
            registry.Register(new WindowsPlugin(), new PlatformActions(catalog));
            var ring = new RingWindow();
            var before = WindowCatalog.Foreground;
            ring.SetMenu(DefaultProfiles.Create()[0], registry);
            ring.ShowAt(600, 420);
            ring.Highlight(RingSlot.Right);
            await Task.Delay(400);
            var noActivate = WindowCatalog.Foreground == before;
            await RenderAsync((FrameworkElement)ring.Content, Path.Combine(directory, "ring.png"));
            ring.HideRing();
            ring.Close();
            var results = new List<string> { noActivate ? "PASS: WinUI rendered; overlay did not activate." : "FAIL: overlay changed foreground." };
            await CheckMiddleGesturesAsync(main, results);
            main.ShowEditorForSmoke();
            await main.EditorForSmoke.CheckOpenSelectionsForSmokeAsync(results);
            main.EditorForSmoke.EditShortcutForSmoke();
            await Task.Delay(300);
            await RenderAsync((FrameworkElement)main.Content, Path.Combine(directory, "editor.png"));
            var edited = main.EditorForSmoke.DraftForSmoke;
            var rightEntry = edited.Profiles.Single(p => p.Applications.Count == 0).Entries.Single(e => e.Slot == RingSlot.Right);
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
        draft.SetShortcut("global", RingSlot.Right, "保存文件", new ushort[] { 0x11, 0x53 });
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

    private sealed class RecordingPlatform : IPlatformActions
    {
        public int Calls { get; private set; }
        public ActionResult SwitchWindow(nint source, WindowDirection direction)
        {
            Calls++;
            return ActionResult.Success("Recorded window switch");
        }
        public List<ushort[]> Shortcuts { get; } = [];
        public ActionResult SendShortcut(nint source, params ushort[] keys)
        {
            Shortcuts.Add(keys.ToArray());
            return ActionResult.Success("Recorded shortcut");
        }
    }
    private static async Task RenderAsync(FrameworkElement element, string path)
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
