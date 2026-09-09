namespace MouseNavigator.Core;

/// <summary>Portable, explicit virtual-key vocabulary used by the editor and JSON validator.</summary>
public static class ShortcutKeys
{
    public static IReadOnlyDictionary<ushort, string> MainKeys { get; } = BuildKeys();
    public static bool IsModifier(ushort key) => key is 0x11 or 0x12 or 0x10 or 0x5B;
    public static void Validate(IReadOnlyList<ushort>? keys)
    {
        if (keys is null || keys.Count is < 1 or > 5 || keys.Distinct().Count() != keys.Count
            || !MainKeys.ContainsKey(keys[^1]) || keys.Take(keys.Count - 1).Any(k => !IsModifier(k)))
            throw new ArgumentException("快捷键需要一个主键，可搭配 Ctrl、Alt、Shift、Win；不能包含重复按键。");
    }
    public static string Format(IReadOnlyList<ushort> keys) => string.Join(" + ", keys.Select(k =>
        k switch { 0x11 => "Ctrl", 0x12 => "Alt", 0x10 => "Shift", 0x5B => "Win", _ => MainKeys.GetValueOrDefault(k, $"0x{k:X2}") }));
    private static IReadOnlyDictionary<ushort, string> BuildKeys()
    {
        var keys = new Dictionary<ushort, string>();
        for (ushort key = 0x41; key <= 0x5A; key++) keys.Add(key, ((char)key).ToString());
        for (ushort key = 0x30; key <= 0x39; key++) keys.Add(key, ((char)key).ToString());
        for (ushort key = 0x70; key <= 0x87; key++) keys.Add(key, $"F{key - 0x6F}");
        foreach (var (key, name) in new (ushort, string)[] {
            (0x08, "Backspace"), (0x09, "Tab"), (0x0D, "Enter"), (0x1B, "Esc"),
            (0x20, "Space"), (0x21, "Page Up"), (0x22, "Page Down"), (0x23, "End"),
            (0x24, "Home"), (0x25, "Left"), (0x26, "Up"), (0x27, "Right"), (0x28, "Down"),
            (0x2D, "Insert"), (0x2E, "Delete"), (0xBA, ";"), (0xBB, "="), (0xBC, ","),
            (0xBD, "-"), (0xBE, "."), (0xBF, "/"), (0xC0, "`"), (0xDB, "["), (0xDC, "\\"),
            (0xDD, "]"), (0xDE, "'") }) keys.Add(key, name);
        return keys;
    }
}