using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MouseNavigator.Contracts;

namespace MouseNavigator.Core;

public static class ConfigurationCodec
{
    public const int MaximumBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter<RingSlot>(allowIntegerValues: false) }
    };

    public static NavigatorConfiguration Deserialize(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaximumBytes) throw new ArgumentException("配置文件不能超过 1 MB。");
        NavigatorConfiguration configuration;
        try { configuration = JsonSerializer.Deserialize<NavigatorConfiguration>(json, Options) ?? throw new ArgumentException("配置文件为空。"); }
        catch (JsonException ex) { throw new ArgumentException("配置文件格式不正确：" + ex.Message, ex); }
        Validate(configuration);
        return configuration;
    }
    public static string Serialize(NavigatorConfiguration configuration)
    {
        Validate(configuration);
        var json = JsonSerializer.Serialize(configuration, Options);
        if (Encoding.UTF8.GetByteCount(json) > MaximumBytes) throw new ArgumentException("配置文件不能超过 1 MB。");
        return json;
    }
    public static void Validate(NavigatorConfiguration configuration)
    {
        if (configuration.SchemaVersion != 1) throw new ArgumentException("不支持此配置版本，请使用版本 1 的菜单配置。");
        if (configuration.Profiles is null || configuration.Profiles.Count is < 1 or > 100
            || configuration.Shortcuts is null || configuration.Shortcuts.Count > 400)
            throw new ArgumentException("配置需要 1–100 个菜单，且快捷键不能超过 400 个。");
        _ = new ProfileResolver(configuration.Profiles);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var shortcut in configuration.Shortcuts)
        {
            if (shortcut is null || !ValidId(shortcut.Id) || !shortcut.Id.StartsWith("shortcuts.", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(shortcut.Name) || shortcut.Name.Length > 80 || !ids.Add(shortcut.Id))
                throw new ArgumentException("自定义快捷键名称或标识无效，或标识重复。");
            ShortcutKeys.Validate(shortcut.Keys);
        }
        foreach (var entry in configuration.Profiles.SelectMany(p => p.Entries))
        {
            if (!ValidId(entry.ActionId)) throw new ArgumentException("动作标识无效。");
            if (entry.ActionId.StartsWith("shortcuts.", StringComparison.Ordinal) && !ids.Contains(entry.ActionId))
                throw new ArgumentException($"缺少快捷键定义：{entry.ActionId}");
        }
    }
    internal static bool ValidId(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 160
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');
}