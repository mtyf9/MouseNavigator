using MouseNavigator.Contracts;
using MouseNavigator.Core;
using MouseNavigator.Actions.BuiltIn;

var tests = new List<(string Name, Action Run)>();
WindowCandidate W(int id, uint pid = 1) => new(new((nint)id, pid), "Window " + id);
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; actual {actual}"); }
void Throws(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Expected ArgumentException"); }
void Add(string name, Action run) => tests.Add((name, run));
var context = new ApplicationContext(1, "Editor", "Document");
Add("Ring geometry reaches every button for counts one through twelve", () =>
{
    for (var count = 1; count <= RingGeometry.MaximumButtons; count++)
        for (var i = 0; i < count; i++)
        {
            var angle = RingGeometry.Angle(i, count) * Math.PI / 180;
            Equal<int?>(i, RingGeometry.HitTest(100 * Math.Cos(angle), 100 * Math.Sin(angle), count));
            var gap = (RingGeometry.Angle(i, count) + 180d / count) * Math.PI / 180;
            Equal<int?>(null, RingGeometry.HitTest(100 * Math.Cos(gap), 100 * Math.Sin(gap), count));
        }
    Equal<int?>(null, RingGeometry.HitTest(0, 0, 4));
    Equal<int?>(null, RingGeometry.HitTest(152, 0, 4));
    Equal<int?>(null, RingGeometry.HitTest(100, 0, 0));
    Equal<int?>(null, RingGeometry.HitTest(100, 0, 13));
});
MenuEntry[] FourButtons() => [new("top", "windows.tasks"), new("right", "windows.maximize"), new("bottom", "windows.minimizeAll"), new("left", "windows.tasks")];
MenuProfile Default() => new(3, "global", "Global", [], FourButtons());
MenuProfile Editor(string id, int priority = 0) => new(3, id, id, [new("editor.exe")], FourButtons(), priority);Add("Application matching is case-insensitive and accepts exe suffix", () =>
    Equal("editor", new ProfileResolver([Default(), Editor("editor")]).Resolve(context with { ProcessName = "EDITOR" }).Id));
Add("Unknown application falls back", () =>
    Equal("global", new ProfileResolver([Default(), Editor("editor")]).Resolve(context with { ProcessName = "" }).Id));
Add("Profile priority and tie break deterministic", () =>
{
    Equal("high", new ProfileResolver([Default(), Editor("low"), Editor("high", 10)]).Resolve(context).Id);
    Equal("a", new ProfileResolver([Default(), Editor("z"), Editor("a")]).Resolve(context).Id);
});
Add("Invalid profile versions rejected", () => Throws(() => new ProfileResolver([Default() with { SchemaVersion = 99 }])));
Add("Duplicate slots rejected", () => Throws(() => new ProfileResolver([Default() with { Entries = [new("left", "a"), new("left", "b")] }])));
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
NavigatorConfiguration Config() => new(3, [Default(), Editor("editor")], []);
Add("Configuration JSON round trip preserves application menus and shortcuts", () =>
{
    var draft = new ConfigurationDraft(Config());
    draft.SetShortcut("editor", "top", "保存", new ushort[] { 0x11, 0x53 });
    var json = ConfigurationCodec.Serialize(draft.Snapshot());
    Equal(json, ConfigurationCodec.Serialize(ConfigurationCodec.Deserialize(json)));
});
Add("Malformed JSON and future versions fail clearly", () =>
{
    Throws(() => ConfigurationCodec.Deserialize("{"));
    Throws(() => ConfigurationCodec.Serialize(Config() with { SchemaVersion = 99 }));
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
    Throws(() => ConfigurationCodec.Validate(Config() with { Profiles = [Default() with { Entries = [new("top", "shortcuts.missing")] }] })));
Add("Unknown plugin reference is preserved for portable menus", () =>
{
    var config = Config() with { Profiles = [Default() with { Entries = [new("top", "thirdparty.action")] }] };
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
    draft.SetShortcut("editor", "right", "保存", new ushort[] { 0x11, 0x53 });
    Equal(before, ConfigurationCodec.Serialize(original));
    Equal("Code", draft.Snapshot().Profiles.Single(p => p.Id == "editor").Name);
});
Add("Copied profile shortcut edits are isolated and unused definitions pruned", () =>
{
    var draft = new ConfigurationDraft(Config());
    draft.SetShortcut("global", "top", "复制", new ushort[] { 0x11, 0x43 });
    var copy = draft.Add();
    draft.SetShortcut(copy, "top", "粘贴", new ushort[] { 0x11, 0x56 });
    Equal(2, draft.Snapshot().Shortcuts.Count);
    Equal("复制", draft.Shortcut(draft.Find("global").Entries.Single(e => e.Id == "top").ActionId)!.Name);
    draft.Delete(copy);
    Equal(1, draft.Snapshot().Shortcuts.Count);
    draft.SetAction("global", "top", null);
    Equal(0, draft.Snapshot().Shortcuts.Count);
});
Add("Only the selected default is protected and unbound menus are allowed", () =>
{
    var draft = new ConfigurationDraft(Config());
    Throws(() => draft.Delete("global"));
    draft.Update("editor", "Editor", [], 0);
    Equal(false, draft.Find("editor").IsDefault);
    draft.SetDefault("editor");draft.Delete("global");
    Equal("editor", draft.Snapshot().Profiles.Single().Id);
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
        Throws(() => store.Save(Config() with { SchemaVersion = 99 }));
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
    var action = profile.Entries.Single(e => e.Id == "top").ActionId;
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
    Equal(true, ConfigurationCodec.Deserialize(json).Profiles.SelectMany(p => p.Entries).Where(e => e.ActionId != "windows.minimizeAll").All(e => e.ActionId == "windows.window.preview"));
});
Add("Version one sparse menus migrate without moving assigned directions", () =>
{
    var legacy = """
    { "schemaVersion":1,"profiles":[{"schemaVersion":1,"id":"global","name":"Old","applications":[],
      "entries":[{"slot":"Left","actionId":"shortcuts.save"},{"slot":"Top","actionId":"windows.window.next"}]}],
      "shortcuts":[{"id":"shortcuts.save","name":"Save","keys":[17,83]}] }
    """;
    var result = ConfigurationCodec.Deserialize(legacy);
    Equal(3, result.SchemaVersion);
    Equal(ConfigurationCodec.Serialize(result), ConfigurationCodec.Serialize(ConfigurationCodec.Deserialize(legacy.Replace("Left", "left").Replace("Top", "top"))));
    Equal("top,right,bottom,left", string.Join(",", result.Profiles[0].Entries.Select(e => e.Id)));
    Equal("windows.window.preview", result.Profiles[0].Entries[0].ActionId);
    Equal("", result.Profiles[0].Entries[1].ActionId);
    Equal("shortcuts.save", result.Profiles[0].Entries[3].ActionId);
    Throws(() => ConfigurationCodec.Deserialize(legacy.Replace("\"slot\":\"Left\"", "\"slot\":\"Top\"")));
    Throws(() => ConfigurationCodec.Deserialize(legacy.Replace("\"slot\":\"Left\"", "\"slot\":\"Left\",\"future\":1")));
    Equal(ConfigurationCodec.Serialize(result), ConfigurationCodec.Serialize(ConfigurationCodec.Deserialize(ConfigurationCodec.Serialize(result))));
});
Add("Added buttons keep identity appearance and action across reorder and round trip", () =>
{
    var draft = new ConfigurationDraft(Config());
    var id = draft.AddButton("global");
    draft.SetShortcut("global", id, "Save", [17,83]);
    draft.SetAppearance("global", id, "My save", "\uE74E");
    draft.CommitDrop("global", new MenuDragSession(draft.Find("global"), id).PreviewAt(0, 3), null, null);
    var config = ConfigurationCodec.Deserialize(ConfigurationCodec.Serialize(draft.Snapshot()));
    var selected = config.Profiles.Single(p => p.Id == "global").Entries[3];
    Equal(id, selected.Id); Equal("My save", selected.Label); Equal("\uE74E", selected.Glyph);
    Equal("Save", config.Shortcuts.Single(s => s.Id == selected.ActionId).Name);
    draft.RemoveButton("global", id);
    Equal(4, draft.Find("global").Entries.Count); Equal(0, draft.Snapshot().Shortcuts.Count);
});
Add("Button count bounds and empty menu are editable", () =>
{
    var draft = new ConfigurationDraft(Config());
    for (var i = 4; i < RingGeometry.MaximumButtons; i++) draft.AddButton("global");
    Equal(12, draft.Snapshot().Profiles.Single(p => p.Id == "global").Entries.Count);
    Throws(() => draft.AddButton("global"));
    foreach (var button in draft.Find("global").Entries.ToArray()) draft.RemoveButton("global", button.Id);
    Equal(0, draft.Snapshot().Profiles.Single(p => p.Id == "global").Entries.Count);
    var added = draft.AddButton("global");
    Equal(added, draft.Find("global").Entries.Single().Id);
    Throws(() => draft.SetAction("global", "missing", "windows.tasks"));
});
Add("Clearing action retains its button and releases unused shortcut", () =>
{
    var draft = new ConfigurationDraft(Config());
    draft.SetShortcut("global", "right", "Save", [17,83]);
    draft.SetAction("global", "right", null);
    Equal(4, draft.Find("global").Entries.Count);
    Equal("", draft.Find("global").Entries.Single(e => e.Id == "right").ActionId);
    Equal(0, draft.Snapshot().Shortcuts.Count);
});
Add("Multiple rings add remove and renumber without modifying surviving buttons",()=>
{
    var draft=new ConfigurationDraft(Config());for(var i=0;i<30;i++)draft.AddRing("global");
    var id=draft.AddButton("global",15);draft.SetAction("global",id,"windows.tasks");
    draft.RemoveRing("global",2);Equal(30,draft.Find("global").RingCount);Equal(14,draft.Find("global").Entries.Single(e=>e.Id==id).Ring);
    while(draft.Find("global").RingCount>0)draft.RemoveRing("global",0);
    Equal(0,draft.Snapshot().Profiles.Single(p=>p.Id=="global").Entries.Count);Equal(0,draft.AddRing("global"));
});
Add("Annular hit testing separates rings angular gaps and center",()=>
{
    var p=Default() with{RingCount=3,Entries=[new("inner","windows.tasks"),new("outer","windows.tasks",Ring:2)]};
    var layout=new MultiRingLayout(p);Equal("inner",layout.HitTest(0,-100));Equal("outer",layout.HitTest(0,-340));
    Equal<string?>(null,layout.HitTest(0,-210));Equal<int?>(1,layout.RingAt(0,-210));
    Equal<int?>(null,layout.RingAt(0,-155));Equal<string?>(null,layout.HitTest(0,0));Equal<string?>(null,layout.HitTest(0,-500));
});
Add("Drag previews are immutable and cross-ring insertions retain attributes",()=>
{
    var p=Default() with{RingCount=2,Entries=[new("a","windows.tasks","A","x"),new("b","windows.maximize","B","y",1)]};
    var drag=new MenuDragSession(p,"a");var preview=drag.Preview(1,"b");
    Equal(0,p.Entries[0].Ring);Equal(1,preview.Entries.Single(e=>e.Id=="a").Ring);Equal("A",preview.Entries.Single(e=>e.Id=="a").Label);
    Equal(0,drag.Preview(0,"a").Entries.Single(e=>e.Id=="a").Ring);
    Equal("a,b",string.Join(",",preview.Entries.Select(e=>e.Id)));Equal(1,preview.Entries.Single(e=>e.Id=="b").Ring);
});
Add("Preset shortcut instances and saved presets remain independent",()=>
{
    var draft=new ConfigurationDraft(Config());draft.AddRing("global");
    var preset=new ButtonPreset("template","Save","shortcuts.template","x",Keys:new ushort[]{17,83});
    var first=new MenuDragSession(draft.Find("global"),null,preset);draft.CommitDrop("global",first.Preview(1,null),preset,first.NewButtonId);
    var saved=draft.SavePreset("global",first.NewButtonId,"Saved","x");
    var second=new MenuDragSession(draft.Find("global"),null,saved);draft.CommitDrop("global",second.Preview(1,null),saved,second.NewButtonId);
    draft.SetShortcut("global",first.NewButtonId,"Copy",new ushort[]{17,67});
    Equal("17,83",string.Join(",",saved.Keys!));Equal("17,83",string.Join(",",draft.Shortcut(draft.Find("global").Entries.Single(e=>e.Id==second.NewButtonId).ActionId)!.Keys));
    var json=ConfigurationCodec.Serialize(draft.Snapshot());Equal(json,ConfigurationCodec.Serialize(ConfigurationCodec.Deserialize(json)));
});
Add("Rejected drops leave draft and shortcut definitions intact",()=>
{
    var draft=new ConfigurationDraft(Config());var before=ConfigurationCodec.Serialize(draft.Snapshot());
    var preset=new ButtonPreset("bad","Bad","shortcuts.bad",Keys:new ushort[]{17});var drag=new MenuDragSession(draft.Find("global"),null,preset);
    Throws(()=>draft.CommitDrop("global",drag.Preview(0,null),preset,drag.NewButtonId));Equal(before,ConfigurationCodec.Serialize(draft.Snapshot()));
});
Add("Embedded icons round trip and invalid image dimensions fail",()=>
{
    const string png="iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aMfoAAAAASUVORK5CYII=";
    var draft=new ConfigurationDraft(Config());draft.SetAppearance("global","top","Image",null,png);
    Equal(png,ConfigurationCodec.Deserialize(ConfigurationCodec.Serialize(draft.Snapshot())).Profiles[0].Entries[0].Image);
    Throws(()=>ConfigurationCodec.ValidateImage("not an image"));var bytes=Convert.FromBase64String(png);bytes[16]=1;Throws(()=>ConfigurationCodec.ValidateImage(Convert.ToBase64String(bytes)));
});
Add("Version two menus migrate into their original inner ring",()=>
{
    const string json="""{"schemaVersion":2,"profiles":[{"schemaVersion":2,"id":"global","name":"Old","applications":[],"entries":[{"id":"a","actionId":"windows.tasks","label":"Task"}]}],"shortcuts":[]}""";
    var migrated=ConfigurationCodec.Deserialize(json);Equal(3,migrated.SchemaVersion);Equal(1,migrated.Profiles[0].RingCount);Equal(0,migrated.Profiles[0].Entries[0].Ring);Equal("Task",migrated.Profiles[0].Entries[0].Label);
});
Add("Insertion shifts all intervening neighbours in either direction",()=>
{
    var p=Default();
    Equal("right,bottom,top,left",string.Join(",",new MenuDragSession(p,"top").PreviewAt(0,2).Entries.Select(e=>e.Id)));
    Equal("left,top,right,bottom",string.Join(",",new MenuDragSession(p,"left").PreviewAt(0,0).Entries.Select(e=>e.Id)));
    Equal("top,right,bottom,left",string.Join(",",p.Entries.Select(e=>e.Id)));
    for(var count=1;count<=12;count++)
        for(var source=0;source<count;source++)
            for(var destination=0;destination<count;destination++)
            {
                var entries=Enumerable.Range(0,count).Select(i=>new MenuEntry("b"+i,"windows.tasks","Name "+i)).ToArray();
                var session=new MenuDragSession(p with{Entries=entries},entries[source].Id);
                var result=session.PreviewAt(0,destination).Entries;
                Equal(entries[source],result[destination]);
                Equal(string.Join(",",entries.Where(e=>e.Id!=session.SourceId).Select(e=>e.Id)),string.Join(",",result.Where(e=>e.Id!=session.SourceId).Select(e=>e.Id)));
            }
});
Add("Preset and cross-ring insertions shift destination without stealing its buttons",()=>
{
    var p=Default() with{RingCount=2};
    p=p with{Entries=p.Entries.Concat(new MenuEntry[]{new("a","windows.tasks",Ring:1),new("b","windows.maximize",Ring:1)}).ToArray()};
    var result=new MenuDragSession(p,"right").PreviewAt(1,1);
    Equal("top,bottom,left",string.Join(",",result.Entries.Where(e=>e.Ring==0).Select(e=>e.Id)));
    Equal("a,right,b",string.Join(",",result.Entries.Where(e=>e.Ring==1).Select(e=>e.Id)));
    var session=new MenuDragSession(p,null,new("preset","Task","windows.tasks"));
    Equal("a,"+session.NewButtonId+",b",string.Join(",",session.PreviewAt(1,1).Entries.Where(e=>e.Ring==1).Select(e=>e.Id)));
    Equal("a,b,"+session.NewButtonId,string.Join(",",session.PreviewAt(1,2).Entries.Where(e=>e.Ring==1).Select(e=>e.Id)));
});
Add("Full destination rejects added or transferred buttons while full-ring reorder works",()=>
{
    var p=Default() with{RingCount=2,Entries=Enumerable.Range(0,12).Select(i=>new MenuEntry("b"+i,"windows.tasks")).Append(new("outer","windows.tasks",Ring:1)).ToArray()};
    Throws(()=>new MenuDragSession(p,"outer").PreviewAt(0,4));
    Throws(()=>new MenuDragSession(p,null,new("preset","Task","windows.tasks")).PreviewAt(0,4));
    Equal("b0",new MenuDragSession(p,"b0").PreviewAt(0,11).Entries.Where(e=>e.Ring==0).Last().Id);
    Equal(13,p.Entries.Count);
});
Add("Every final insertion slot is reachable including append and empty rings",()=>
{
    for(var count=0;count<12;count++)
    {
        var p=Default() with{Entries=Enumerable.Range(0,count).Select(i=>new MenuEntry("b"+i,"windows.tasks")).ToArray()};
        var session=new MenuDragSession(p,null,new("preset","Task","windows.tasks"));
        for(var index=0;index<=count;index++)
        {
            var radians=RingGeometry.Angle(index,count+1)*Math.PI/180;
            Equal(index,session.InsertionIndex(0,100*Math.Cos(radians),100*Math.Sin(radians)));
        }
    }
});
Add("Center content is per-menu portable and independently resettable",()=>
{
    const string png="iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aMfoAAAAASUVORK5CYII=";
    var draft=new ConfigurationDraft(Config());draft.SetCenterAppearance("global","我的菜单",png);
    var result=ConfigurationCodec.Deserialize(ConfigurationCodec.Serialize(draft.Snapshot()));
    Equal("我的菜单",result.Profiles[0].CenterText);Equal(png,result.Profiles[0].CenterImage);Equal<string?>(null,result.Profiles[1].CenterText);
    draft.SetCenterAppearance("global",null,null);Equal<string?>(null,draft.Find("global").CenterImage);
    Throws(()=>draft.SetCenterAppearance("global",new string('x',41),null));
    Throws(()=>draft.SetCenterAppearance("global","Text","bad image"));
    Throws(()=>ConfigurationCodec.Validate(Config() with{Profiles=[Default() with{CenterText=new string('x',41)}]}));
    Throws(()=>ConfigurationCodec.Validate(Config() with{Profiles=[Default() with{CenterImage="bad image"}]}));
});
Add("Existing version three menus without center fields retain default center",()=>
{
    const string json="""{"schemaVersion":3,"profiles":[{"schemaVersion":3,"id":"global","name":"Old","applications":[],"entries":[],"ringCount":1}],"shortcuts":[]}""";
    var p=ConfigurationCodec.Deserialize(json).Profiles[0];Equal<string?>(null,p.CenterText);Equal<string?>(null,p.CenterImage);
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
