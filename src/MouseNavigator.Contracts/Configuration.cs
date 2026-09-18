namespace MouseNavigator.Contracts;
public sealed record NavigatorConfiguration(int SchemaVersion,
    IReadOnlyList<MenuProfile> Profiles, IReadOnlyList<ShortcutDefinition> Shortcuts,
    IReadOnlyList<ButtonPreset>? Presets = null, IReadOnlyList<PresetFolder>? PresetFolders = null, IReadOnlyList<string>? PresetOrder = null, bool BuiltInMenusInitialized = false, TriggerSettings? Trigger = null);
public sealed record PresetFolder(string Id,string Name,string? ParentId=null);
public sealed record ShortcutDefinition(string Id, string Name, IReadOnlyList<ushort> Keys);
public sealed record ButtonPreset(string Id, string Name, string ActionId, string? Glyph = null,
    string? Image = null, IReadOnlyList<ushort>? Keys = null, ApplicationLaunch? Launch = null, MacroDefinition? Macro = null, string? FolderId = null);
public sealed record MenuDocument(int SchemaVersion, MenuProfile Menu, IReadOnlyList<ShortcutDefinition> Shortcuts);
public enum TriggerMode { Hold, Toggle }
public enum TriggerDevice { MiddleMouse, MouseX1, MouseX2, Keyboard }
public sealed record TriggerSettings(TriggerMode Mode=TriggerMode.Hold,TriggerDevice Device=TriggerDevice.MiddleMouse,ushort Key=119,int HoldMilliseconds=300);
