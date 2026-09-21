namespace MouseNavigator.Contracts;
public sealed record NavigatorConfiguration(int SchemaVersion,
    IReadOnlyList<MenuProfile> Profiles, IReadOnlyList<ShortcutDefinition> Shortcuts,
    IReadOnlyList<ButtonPreset>? Presets = null, IReadOnlyList<PresetFolder>? PresetFolders = null, IReadOnlyList<string>? PresetOrder = null, bool BuiltInMenusInitialized = false, TriggerSettings? Trigger = null, IReadOnlyList<BackgroundPreset>? BackgroundPresets = null, IReadOnlyList<string>? BlockedApplications = null, IReadOnlyList<AppearancePreset>? AppearancePresets = null);
public sealed record PresetFolder(string Id,string Name,string? ParentId=null);
public sealed record ShortcutDefinition(string Id, string Name, IReadOnlyList<ushort> Keys);
public sealed record ButtonPreset(string Id, string Name, string ActionId, string? Glyph = null,
    string? Image = null, IReadOnlyList<ushort>? Keys = null, ApplicationLaunch? Launch = null, MacroDefinition? Macro = null, string? FolderId = null);
public sealed record MenuDocument(int SchemaVersion, MenuProfile Menu, IReadOnlyList<ShortcutDefinition> Shortcuts);
public enum TriggerMode { Hold, Toggle }
public enum TriggerDevice { MiddleMouse, MouseX1, MouseX2, Keyboard }
public sealed record TriggerSettings(TriggerMode Mode=TriggerMode.Hold,TriggerDevice Device=TriggerDevice.MiddleMouse,ushort Key=119,int HoldMilliseconds=300);
public sealed record BackgroundPreset(string Id,string Name,string? Image=null);
public sealed record AppearancePreset(string Id,string Name,MenuAppearance Appearance);
public sealed record MenuAppearance(MenuVisualStyle? Background, string? AccentColor,string? NormalColor,double ActiveOpacity,double NormalOpacity,double ButtonGap,double SizeScale,string? CenterText,string? CenterImage,string? CenterGlyph,WindowPreviewAppearance? WindowPreview)
{
    public static MenuAppearance From(MenuProfile menu)=>new(menu.VisualStyle,menu.AccentColor,menu.NormalColor,menu.ActiveOpacity,menu.NormalOpacity,menu.ButtonGap,menu.SizeScale,menu.CenterText,menu.CenterImage,menu.CenterGlyph,menu.PreviewAppearance);
    public MenuProfile Apply(MenuProfile menu)=>menu with{VisualStyle=Background,AccentColor=AccentColor,NormalColor=NormalColor,ActiveOpacity=ActiveOpacity,NormalOpacity=NormalOpacity,ButtonGap=ButtonGap,SizeScale=SizeScale,CenterText=CenterText,CenterImage=CenterImage,CenterGlyph=CenterGlyph,PreviewAppearance=WindowPreview};
}
