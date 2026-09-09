using MouseNavigator.Contracts;
using MouseNavigator.Core;
using MouseNavigator.Actions.BuiltIn;

var tests = new List<(string Name, Action Run)>();
WindowCandidate W(int id, uint pid = 1) => new(new((nint)id, pid), "Window " + id);
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; actual {actual}"); }
void Throws(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Expected ArgumentException"); }
void Add(string name, Action run) => tests.Add((name, run));
var context = new ApplicationContext(1, "Editor", "Document");
Add("Ring directions and cancellation", () =>
{
    Equal<RingSlot?>(RingSlot.Right, RingGeometry.HitTest(100, 0));
    Equal<RingSlot?>(RingSlot.Left, RingGeometry.HitTest(-100, 0));
    Equal<RingSlot?>(RingSlot.Top, RingGeometry.HitTest(0, -100));
    Equal<RingSlot?>(RingSlot.Bottom, RingGeometry.HitTest(0, 100));
    Equal<RingSlot?>(null, RingGeometry.HitTest(0, 0));
    Equal<RingSlot?>(null, RingGeometry.HitTest(158, 0));
});
MenuProfile Default() => new(1, "global", "Global", [], [new(RingSlot.Left, "windows.tasks")]);
MenuProfile Editor(string id, int priority = 0) => new(1, id, id, [new("editor.exe")], [new(RingSlot.Right, "windows.maximize")], priority);
Add("Application matching is case-insensitive and accepts exe suffix", () =>
    Equal("editor", new ProfileResolver([Default(), Editor("editor")]).Resolve(context with { ProcessName = "EDITOR" }).Id));
Add("Unknown application falls back", () =>
    Equal("global", new ProfileResolver([Default(), Editor("editor")]).Resolve(context with { ProcessName = "" }).Id));
Add("Profile priority and tie break deterministic", () =>
{
    Equal("high", new ProfileResolver([Default(), Editor("low"), Editor("high", 10)]).Resolve(context).Id);
    Equal("a", new ProfileResolver([Default(), Editor("z"), Editor("a")]).Resolve(context).Id);
});
Add("Invalid profile versions rejected", () => Throws(() => new ProfileResolver([Default() with { SchemaVersion = 2 }])));
Add("Duplicate slots rejected", () => Throws(() => new ProfileResolver([Default() with { Entries = [new(RingSlot.Left, "a"), new(RingSlot.Left, "b")] }])));
Add("Duplicate profile IDs rejected", () => Throws(() => new ProfileResolver([Default(), Default()])));
Add("Missing fallback rejected", () => Throws(() => new ProfileResolver([Editor("e")])));
Add("Shortcut plugin captures the supplied platform", () =>
{
    var platform = new FakePlatform(); var registry = new ActionRegistry();
    registry.Register(new WindowsPlugin(), platform);
    Equal(true, registry.ExecuteAsync("windows.tasks", context).AsTask().GetAwaiter().GetResult().Succeeded);
    Equal("91,9", string.Join(",", platform.Keys));
});
Add("Missing plugin action is a visible failure", () =>
    Equal(false, new ActionRegistry().ExecuteAsync("unknown.action", context).AsTask().GetAwaiter().GetResult().Succeeded));
Add("Duplicate plugin registration rejected atomically", () =>
{
    var registry = new ActionRegistry(); var platform = new FakePlatform();
    registry.Register(new WindowsPlugin(), platform);
    Throws(() => registry.Register(new WindowsPlugin(), platform));
    Equal(6, registry.Actions.Count);
});
Add("Canceled action does not reach native platform", () =>
{
    var registry = new ActionRegistry(); var platform = new FakePlatform();
    registry.Register(new WindowsPlugin(), platform);
    Equal(false, registry.ExecuteAsync("windows.maximize", context, new CancellationToken(true)).AsTask().GetAwaiter().GetResult().Succeeded);
    Equal((nint)0, platform.Source);
});
Add("Plugin failures do not escape host", () =>
{
    var registry = new ActionRegistry(); registry.Register(new ThrowingPlugin(), new FakePlatform());
    Equal(false, registry.ExecuteAsync("throw.action", context).AsTask().GetAwaiter().GetResult().Succeeded);
});
NavigatorConfiguration Config() => new(1, [Default(), Editor("editor")], []);
Add("Configuration JSON round trip preserves application menus and shortcuts", () =>
{
    var draft = new ConfigurationDraft(Config());
    draft.SetShortcut("editor", RingSlot.Top, "保存", new ushort[] { 0x11, 0x53 });
    var json = ConfigurationCodec.Serialize(draft.Snapshot());
    Equal(json, ConfigurationCodec.Serialize(ConfigurationCodec.Deserialize(json)));
});
Add("Malformed JSON and future versions fail clearly", () =>
{
    Throws(() => ConfigurationCodec.Deserialize("{"));
    Throws(() => ConfigurationCodec.Serialize(Config() with { SchemaVersion = 2 }));
    Throws(() => ConfigurationCodec.Deserialize("{}"));
    Throws(() => ConfigurationCodec.Deserialize("null"));
});
Add("Null lists and null profile entries are rejected", () =>
{
    Throws(() => ConfigurationCodec.Validate(Config() with { Profiles = null! }));
    Throws(() => ConfigurationCodec.Validate(Config() with { Profiles = [null!] }));
    Throws(() => ConfigurationCodec.Validate(Config() with { Profiles = [Default() with { Entries = [null!] }] }));
    Throws(() => ConfigurationCodec.Validate(Config() with { Shortcuts = [null!] }));
});
Add("Unknown JSON fields do not silently disappear", () =>
{
    var json = ConfigurationCodec.Serialize(Config()).Insert(1, System.Text.Json.JsonSerializer.Serialize("futureSetting") + ":true,");
    Throws(() => ConfigurationCodec.Deserialize(json));
});
Add("Oversized configuration rejected", () =>
    Throws(() => ConfigurationCodec.Deserialize(new string(' ', ConfigurationCodec.MaximumBytes + 1))));
Add("Empty menu is a valid disabled ring", () =>
    Equal(0, ConfigurationCodec.Deserialize(ConfigurationCodec.Serialize(Config() with { Profiles = [Default() with { Entries = [] }] })).Profiles[0].Entries.Count));
Add("Empty app match cannot become an accidental fallback", () =>
    Throws(() => new ProfileResolver([Default(), Editor("e") with { Applications = [new("")] }])) );
Add("Executable paths are rejected but surrounding spaces normalize", () =>
{
    Throws(() => new ProfileResolver([Default(), Editor("e") with { Applications = [new("C:\\app.exe")] }]));
    Equal("e", new ProfileResolver([Default(), Editor("e") with { Applications = [new(" EDITOR.exe ")] }]).Resolve(context).Id);
});
Add("Missing shortcut reference is rejected", () =>
    Throws(() => ConfigurationCodec.Validate(Config() with { Profiles = [Default() with { Entries = [new(RingSlot.Top, "shortcuts.missing")] }] })));
Add("Unknown plugin reference is preserved for portable menus", () =>
{
    var config = Config() with { Profiles = [Default() with { Entries = [new(RingSlot.Top, "thirdparty.action")] }] };
    Equal("thirdparty.action", ConfigurationCodec.Deserialize(ConfigurationCodec.Serialize(config)).Profiles[0].Entries[0].ActionId);
});
Add("Shortcut validation rejects duplicates, modifiers-only and reversed chords", () =>
{
    Throws(() => ShortcutKeys.Validate(new ushort[] { 0x11, 0x11, 0x53 }));
    Throws(() => ShortcutKeys.Validate(new ushort[] { 0x11 }));
    Throws(() => ShortcutKeys.Validate(new ushort[] { 0x53, 0x11 }));
    Throws(() => ShortcutKeys.Validate(new ushort[] { 0x01 }));
    ShortcutKeys.Validate(new ushort[] { 0x11, 0x12, 0x10, 0x5B, 0x53 });
    ShortcutKeys.Validate(new ushort[] { 0x74 });
    Equal("Ctrl + Shift + S", ShortcutKeys.Format(new ushort[] { 0x11, 0x10, 0x53 }));
});
Add("Draft changes do not mutate the active configuration", () =>
{
    var original = Config();
    var before = ConfigurationCodec.Serialize(original);
    var draft = new ConfigurationDraft(original);
    draft.Update("editor", "Code", new[] { new ApplicationMatch("code.exe") }, 20);
    draft.SetShortcut("editor", RingSlot.Right, "保存", new ushort[] { 0x11, 0x53 });
    Equal(before, ConfigurationCodec.Serialize(original));
    Equal("Code", draft.Snapshot().Profiles.Single(p => p.Id == "editor").Name);
});
Add("Copied profile shortcut edits are isolated and unused definitions pruned", () =>
{
    var draft = new ConfigurationDraft(Config());
    draft.SetShortcut("global", RingSlot.Top, "复制", new ushort[] { 0x11, 0x43 });
    var copy = draft.Add();
    draft.SetShortcut(copy, RingSlot.Top, "粘贴", new ushort[] { 0x11, 0x56 });
    Equal(2, draft.Snapshot().Shortcuts.Count);
    Equal("复制", draft.Shortcut(draft.Find("global").Entries.Single(e => e.Slot == RingSlot.Top).ActionId)!.Name);
    draft.Delete(copy);
    Equal(1, draft.Snapshot().Shortcuts.Count);
    draft.SetAction("global", RingSlot.Top, null);
    Equal(0, draft.Snapshot().Shortcuts.Count);
});
Add("Global menu cannot be deleted or converted to an application menu", () =>
{
    var draft = new ConfigurationDraft(Config());
    Throws(() => draft.Delete("global"));
    Throws(() => draft.Update("global", "Global", new[] { new ApplicationMatch("app.exe") }, 0));
    Throws(() => draft.Update("editor", "Editor", [], 0));
});
Add("Configured shortcut routes original source and ordered keys through plugin", () =>
{
    var platform = new FakePlatform();
    var registry = new ActionRegistry();
    registry.Register(new ConfiguredShortcutsPlugin(new[] { new ShortcutDefinition("shortcuts.save", "保存", new ushort[] { 0x11, 0x10, 0x53 }) }), platform);
    Equal(true, registry.ExecuteAsync("shortcuts.save", context).AsTask().GetAwaiter().GetResult().Succeeded);
    Equal(context.WindowHandle, platform.Source);
    Equal("17,16,83", string.Join(",", platform.Keys));
});
Add("Configuration save creates backup and rejected writes preserve current file", () =>
{
    WithStore(store =>
    {
        store.Save(Config());
        var previous = File.ReadAllText(store.FilePath);
        store.Save(Config() with { Profiles = [Default() with { Name = "修改后" }] });
        Equal(previous, File.ReadAllText(store.FilePath + ".bak"));
        var valid = File.ReadAllText(store.FilePath);
        Throws(() => store.Save(Config() with { SchemaVersion = 2 }));
        Equal(valid, File.ReadAllText(store.FilePath));
        Equal("修改后", store.Load(Config).Configuration.Profiles[0].Name);
    });
});
Add("Corrupt configuration loads valid backup and preserves evidence until save", () =>
{
    WithStore(store =>
    {
        store.Save(Config());
        store.Save(Config() with { Profiles = [Default() with { Name = "修改后" }] });
        var backup = File.ReadAllText(store.FilePath + ".bak");
        File.WriteAllText(store.FilePath, "{bad json");
        var loaded = store.Load(Config);
        Equal(true, loaded.Warning is not null);
        Equal("{bad json", File.ReadAllText(store.FilePath));
        Equal(backup, ConfigurationCodec.Serialize(loaded.Configuration));
        store.Save(loaded.Configuration);
        Equal(backup, File.ReadAllText(store.FilePath + ".bak"));
    });
});
Add("First run and unrecoverable files load defaults without overwriting files", () =>
{
    WithStore(store =>
    {
        Equal<string?>(null, store.Load(Config).Warning);
        Equal(false, File.Exists(store.FilePath));
        File.WriteAllText(store.FilePath, "{}");
        Equal(true, store.Load(Config).Warning is not null);
        Equal("{}", File.ReadAllText(store.FilePath));
    });
});
void WithStore(Action<ConfigurationStore> run)
{
    var directory = Path.Combine(Path.GetTempPath(), "MouseNavigator.Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try { run(new ConfigurationStore(Path.Combine(directory, "settings.json"))); }
    finally { Directory.Delete(directory, true); }
}
Add("Published example imports and matches Visual Studio with its save shortcut", () =>
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "examples", "menus.example.json")))
        directory = directory.Parent;
    if (directory is null) throw new Exception("Example configuration not found");
    var example = ConfigurationStore.Read(Path.Combine(directory.FullName, "examples", "menus.example.json"));
    var profile = new ProfileResolver(example.Profiles).Resolve(context with { ProcessName = "devenv.exe" });
    Equal("editor", profile.Id);
    var action = profile.Entries.Single(e => e.Slot == RingSlot.Top).ActionId;
    Equal("Ctrl + S", ShortcutKeys.Format(example.Shortcuts.Single(s => s.Id == action).Keys));
});
Add("Preview action declares held interaction instead of invoking a shortcut", () =>
{
    var registry = new ActionRegistry(); var platform = new FakePlatform();
    registry.Register(new WindowsPlugin(), platform);
    Equal(ActionInteraction.WindowPreview, registry.Find("windows.window.preview")!.Descriptor.Interaction);
    Equal(false, registry.ExecuteAsync("windows.window.preview", context).AsTask().GetAwaiter().GetResult().Succeeded);
    Equal(0, platform.Keys.Length);
});
Add("Preview freezes order and removes duplicate identities", () =>
{
    var preview = new WindowPreviewSession([W(3), W(1), W(3), W(2)], new(980, 640));
    preview.RefreshAvailable([W(2), W(4), W(1), W(3)]);
    Equal("3,1,2", string.Join(",", preview.Windows.Select(w => w.Identity.Handle)));
});
Add("Preview closed targets become empty without shifting adjacent cards", () =>
{
    var preview = new WindowPreviewSession([W(1), W(2), W(3)], new(980, 640));
    preview.RefreshAvailable([W(1), W(3)]);
    var second = preview.Layout.Card(1); var third = preview.Layout.Card(2);
    Equal<WindowCandidate?>(null, preview.HitTest(second.X + 20, second.Y + 20));
    Equal((nint)3, preview.HitTest(third.X + 20, third.Y + 20)!.Identity.Handle);
});
Add("Preview rejects handle reuse by another process", () =>
{
    var preview = new WindowPreviewSession([W(1)], new(980, 640));
    preview.RefreshAvailable([W(1, 9)]);
    var card = preview.Layout.Card(0);
    Equal<WindowCandidate?>(null, preview.HitTest(card.X + 10, card.Y + 10));
});
Add("Preview header, gaps and outside positions never select a window", () =>
{
    var preview = new WindowPreviewSession([W(1), W(2)], new(980, 640));
    Equal<WindowCandidate?>(null, preview.HitTest(100, 32));
    Equal<WindowCandidate?>(null, preview.HitTest(-1, 100));
    var first = preview.Layout.Card(0);
    Equal<WindowCandidate?>(null, preview.HitTest(first.X + first.Width + 2, first.Y + 20));
    Equal<WindowCandidate?>(null, preview.HitTest(30, 610));
});
Add("Preview pagination reaches every window and cannot leave bounds", () =>
{
    var preview = new WindowPreviewSession(Enumerable.Range(1, 37).Select(i => W(i)), new(980, 640));
    var visited = new List<nint>();
    do { visited.AddRange(preview.Visible.Select(w => w.Window.Identity.Handle)); }
    while (preview.TurnPage(1));
    Equal(37, visited.Count);
    Equal(37, visited.Distinct().Count());
    Equal(false, preview.TurnPage(1));
    while (preview.TurnPage(-1)) { }
    Equal(0, preview.Page);
    Equal(false, preview.TurnPage(-1));
});
Add("Preview empty list still has a safe cancel page", () =>
{
    var preview = new WindowPreviewSession([], new(980, 640));
    Equal(1, preview.PageCount);
    Equal(false, preview.TurnPage(1));
    Equal<WindowCandidate?>(null, preview.HitTest(100, 100));
});
Add("Preview cards fit narrow and high-DPI work areas", () =>
{
    foreach (var size in new[] { (240d, 240d), (400d, 320d), (620d, 480d), (980d, 640d) })
    {
        var layout = new WindowPreviewLayout(size.Item1, size.Item2);
        for (var slot = 0; slot < layout.Capacity; slot++)
        {
            var rect = layout.Card(slot);
            Equal(true, rect.X >= 0 && rect.Y >= 72 && rect.X + rect.Width <= layout.Width);
            Equal(true, rect.Y + rect.Height <= layout.Previous.Y);
        }
    }
});
Add("Legacy cycle bindings migrate to the window picker", () =>
{
    var json = ConfigurationCodec.Serialize(Config()).Replace("windows.maximize", "windows.window.next").Replace("windows.tasks", "windows.window.previous");
    Equal(true, ConfigurationCodec.Deserialize(json).Profiles.SelectMany(p => p.Entries).All(e => e.ActionId == "windows.window.preview"));
});
var failed = 0;
foreach (var (name, run) in tests)
{
    try { run(); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
}
Console.WriteLine($"{tests.Count - failed}/{tests.Count} passed");
return failed == 0 ? 0 : 1;

sealed class FakePlatform : IPlatformActions
{
    public nint Source;
    public ushort[] Keys = [];
    public ActionResult SendShortcut(nint source, params ushort[] keys) { Source = source; Keys = keys; return ActionResult.Success("ok"); }
}
sealed class ThrowingPlugin : INavigatorPlugin
{
    public string Id => "throw"; public int ApiVersion => 1;
    public IReadOnlyList<INavigatorAction> CreateActions(IPlatformActions platform) => [new ThrowingAction()];
    sealed class ThrowingAction : INavigatorAction
    {
        public ActionDescriptor Descriptor => new("throw.action", "Throw", "", "");
        public ValueTask<ActionResult> ExecuteAsync(ApplicationContext context, CancellationToken cancellationToken) => throw new InvalidOperationException("test failure");
    }
}
