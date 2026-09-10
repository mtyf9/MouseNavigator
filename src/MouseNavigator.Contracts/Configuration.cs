namespace MouseNavigator.Contracts;
public sealed record NavigatorConfiguration(int SchemaVersion,
    IReadOnlyList<MenuProfile> Profiles, IReadOnlyList<ShortcutDefinition> Shortcuts,
    IReadOnlyList<ButtonPreset>? Presets = null);
public sealed record ShortcutDefinition(string Id, string Name, IReadOnlyList<ushort> Keys);
public sealed record ButtonPreset(string Id, string Name, string ActionId, string? Glyph = null,
    string? Image = null, IReadOnlyList<ushort>? Keys = null);
public sealed record MenuDocument(int SchemaVersion, MenuProfile Menu, IReadOnlyList<ShortcutDefinition> Shortcuts);
