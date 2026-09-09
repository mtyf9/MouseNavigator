namespace MouseNavigator.Contracts;

public sealed record NavigatorConfiguration(int SchemaVersion,
    IReadOnlyList<MenuProfile> Profiles, IReadOnlyList<ShortcutDefinition> Shortcuts);
public sealed record ShortcutDefinition(string Id, string Name, IReadOnlyList<ushort> Keys);