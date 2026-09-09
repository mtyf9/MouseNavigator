using MouseNavigator.Contracts;
using MouseNavigator.Core;
using MouseNavigator.Actions.BuiltIn;

var tests = new List<(string Name, Action Run)>();
WindowCandidate W(int id, uint pid = 1) => new(new((nint)id, pid), "Window " + id);
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; actual {actual}"); }
void Throws(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Expected ArgumentException"); }
void Add(string name, Action run) => tests.Add((name, run));
var context = new ApplicationContext(1, "Editor", "Document");
Add("Empty window list is safe", () => Equal<WindowCandidate?>(null, new WindowCycle().Select([], 1, WindowDirection.Next)));
Add("Single current window is a no-op", () => Equal<WindowCandidate?>(null, new WindowCycle().Select([W(1)], 1, WindowDirection.Next)));
Add("Single other window can activate", () => Equal((nint)2, new WindowCycle().Select([W(2)], 1, WindowDirection.Next)!.Identity.Handle));
Add("Forward traverses A B C A despite Z-order changes", () =>
{
    var cycle = new WindowCycle();
    Equal((nint)2, cycle.Select([W(1), W(2), W(3)], 1, WindowDirection.Next)!.Identity.Handle);
    Equal((nint)3, cycle.Select([W(2), W(1), W(3)], 2, WindowDirection.Next)!.Identity.Handle);
    Equal((nint)1, cycle.Select([W(3), W(2), W(1)], 3, WindowDirection.Next)!.Identity.Handle);
});
Add("Previous reverses forward", () =>
{
    var cycle = new WindowCycle();
    cycle.Select([W(1), W(2), W(3)], 1, WindowDirection.Next);
    Equal((nint)1, cycle.Select([W(2), W(1), W(3)], 2, WindowDirection.Previous)!.Identity.Handle);
    Equal((nint)3, cycle.Select([W(1), W(2), W(3)], 1, WindowDirection.Previous)!.Identity.Handle);
});
Add("Closed windows are removed", () =>
{
    var cycle = new WindowCycle();
    cycle.Select([W(1), W(2), W(3)], 1, WindowDirection.Next);
    Equal((nint)3, cycle.Select([W(1), W(3)], 1, WindowDirection.Next)!.Identity.Handle);
});
Add("New windows append without reordering", () =>
{
    var cycle = new WindowCycle();
    cycle.Select([W(1), W(2)], 1, WindowDirection.Next);
    Equal((nint)3, cycle.Select([W(3), W(2), W(1)], 2, WindowDirection.Next)!.Identity.Handle);
});
Add("Handle reuse with a new process is a new entry", () =>
{
    var cycle = new WindowCycle();
    cycle.Select([W(1), W(2), W(3)], 1, WindowDirection.Next);
    Equal((nint)3, cycle.Select([W(2, 7), W(1), W(3)], 1, WindowDirection.Next)!.Identity.Handle);
});
Add("Unknown source has deterministic direction", () =>
{
    Equal((nint)1, new WindowCycle().Select([W(1), W(2)], 99, WindowDirection.Next)!.Identity.Handle);
    Equal((nint)2, new WindowCycle().Select([W(1), W(2)], 99, WindowDirection.Previous)!.Identity.Handle);
});
Add("Duplicate windows do not duplicate navigation", () =>
    Equal((nint)2, new WindowCycle().Select([W(1), W(1), W(2)], 1, WindowDirection.Next)!.Identity.Handle));
Add("Ring directions and cancellation", () =>
{
    Equal<RingSlot?>(RingSlot.Right, RingGeometry.HitTest(100, 0));
    Equal<RingSlot?>(RingSlot.Left, RingGeometry.HitTest(-100, 0));
    Equal<RingSlot?>(RingSlot.Top, RingGeometry.HitTest(0, -100));
    Equal<RingSlot?>(RingSlot.Bottom, RingGeometry.HitTest(0, 100));
    Equal<RingSlot?>(null, RingGeometry.HitTest(0, 0));
    Equal<RingSlot?>(null, RingGeometry.HitTest(158, 0));
});
MenuProfile Default() => new(1, "global", "Global", [], [new(RingSlot.Left, "windows.window.previous")]);
MenuProfile Editor(string id, int priority = 0) => new(1, id, id, [new("editor.exe")], [new(RingSlot.Right, "windows.window.next")], priority);
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
Add("Plugin routes next and previous with original target", () =>
{
    var platform = new FakePlatform(); var registry = new ActionRegistry();
    registry.Register(new WindowsPlugin(), platform);
    Equal(true, registry.ExecuteAsync("windows.window.next", context).AsTask().GetAwaiter().GetResult().Succeeded);
    Equal((nint)1, platform.Source); Equal(WindowDirection.Next, platform.Direction);
    registry.ExecuteAsync("windows.window.previous", context).AsTask().GetAwaiter().GetResult();
    Equal(WindowDirection.Previous, platform.Direction);
});
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
    Equal(7, registry.Actions.Count);
});
Add("Canceled action does not reach native platform", () =>
{
    var registry = new ActionRegistry(); var platform = new FakePlatform();
    registry.Register(new WindowsPlugin(), platform);
    Equal(false, registry.ExecuteAsync("windows.window.next", context, new CancellationToken(true)).AsTask().GetAwaiter().GetResult().Succeeded);
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
    public WindowDirection Direction;
    public ushort[] Keys = [];
    public ActionResult SwitchWindow(nint source, WindowDirection direction) { Source = source; Direction = direction; return ActionResult.Success("ok"); }
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
