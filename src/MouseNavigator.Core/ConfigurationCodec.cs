using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using MouseNavigator.Contracts;

namespace MouseNavigator.Core;

public static class ConfigurationCodec
{
    public const int MaximumBytes = 16 * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public static NavigatorConfiguration Deserialize(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaximumBytes) throw new ArgumentException("配置文件不能超过 16 MB。");
        NavigatorConfiguration configuration;
        try { configuration = JsonSerializer.Deserialize<NavigatorConfiguration>(Upgrade(json), Options) ?? throw new ArgumentException("配置文件为空。"); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        { throw new ArgumentException("配置文件格式不正确：" + ex.Message, ex); }
        Validate(configuration);
        // Read-only migration: old cycle bindings now open the explicit window picker.
        return configuration with { Profiles = configuration.Profiles.Select(p => p with {
            IsGlobalDefault = p.IsDefault,
            Entries = p.Entries.Select(e => e.ActionId is "windows.window.previous" or "windows.window.next"
                ? e with { ActionId = "windows.window.preview" } : e).ToArray() }).ToArray() };
    }
    public static string Serialize(NavigatorConfiguration configuration)
    {
        Validate(configuration);
        var json = JsonSerializer.Serialize(configuration with { Profiles = configuration.Profiles.Select(p=>p with{IsGlobalDefault=p.IsDefault}).ToArray() }, Options);
        if (Encoding.UTF8.GetByteCount(json) > MaximumBytes) throw new ArgumentException("配置文件不能超过 16 MB。");
        return json;
    }
    public static void Validate(NavigatorConfiguration configuration)
    {
        if (configuration.SchemaVersion != 3) throw new ArgumentException("不支持此配置版本，请使用版本 3 的菜单配置。");
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
        var presets = configuration.Presets ?? [];
        var presetIds = new HashSet<string>();
        foreach (var preset in presets)
        {
            if (preset is null || !ValidId(preset.Id) || !presetIds.Add(preset.Id) || string.IsNullOrWhiteSpace(preset.Name)
                || preset.Name.Length > 80 || (preset.ActionId is null || (preset.ActionId.Length > 0 && !ValidId(preset.ActionId))) || preset.Glyph?.Length > 8)
                throw new ArgumentException("预设按钮无效。");
            if (preset.Keys is not null) ShortcutKeys.Validate(preset.Keys);
            else if (preset.ActionId.StartsWith("shortcuts.")) throw new ArgumentException("快捷键预设缺少按键。");
            ValidateImage(preset.Image);
        }
        foreach (var profile in configuration.Profiles)
        {
            if (profile.CenterText?.Length > 40) throw new ArgumentException("中心文字不能超过 40 个字符。");
            if (profile.CenterGlyph?.Length > 8) throw new ArgumentException("中心图标无效。");
            ValidateImage(profile.CenterImage);
            if (!double.IsFinite(profile.SizeScale) || profile.SizeScale is < 0.5 or > 2) throw new ArgumentException("悬浮窗大小必须为 50% 到 200%。");
            if(profile.RingRotations is not null && profile.RingRotations.Any(pair=>pair.Key<0||pair.Key>=profile.RingCount||!double.IsFinite(pair.Value)||pair.Value<0||pair.Value>=360))
                throw new ArgumentException("圈旋转角度无效。");
        }
        foreach (var entry in configuration.Profiles.SelectMany(p => p.Entries))
        {
            ValidateImage(entry.Image);
            if (entry.ActionId.Length == 0) continue;
            if (!ValidId(entry.ActionId)) throw new ArgumentException("动作标识无效。");
            if (entry.ActionId.StartsWith("shortcuts.", StringComparison.Ordinal) && !ids.Contains(entry.ActionId))
                throw new ArgumentException($"缺少快捷键定义：{entry.ActionId}");
        }
    }
    private static string Upgrade(string json)
    {
        var root = JsonNode.Parse(json) as JsonObject ?? throw new ArgumentException("配置需要一个 JSON 对象。");
        if (root["schemaVersion"]?.GetValue<int>() == 2) return UpgradeTwo(root);
        if (root["schemaVersion"]?.GetValue<int>() != 1) return json;
        root["schemaVersion"] = 2;
        if (root["profiles"] is not JsonArray profiles) throw new ArgumentException("缺少菜单列表。");
        foreach (var node in profiles)
        {
            if (node is not JsonObject profile || profile["schemaVersion"]?.GetValue<int>() != 1
                || profile["entries"] is not JsonArray entries) throw new ArgumentException("旧菜单结构无效。");
            profile["schemaVersion"] = 2;
            var byId = new Dictionary<string, JsonObject>();
            foreach (var item in entries)
            {
                if (item is not JsonObject entry || entry["slot"] is not JsonValue slotNode
                    || !slotNode.TryGetValue<string>(out var slot) || slot.ToLowerInvariant() is not ("top" or "right" or "bottom" or "left")
                    || entry.ContainsKey("id") || entry.ContainsKey("label") || entry.ContainsKey("glyph"))
                    throw new ArgumentException("旧菜单方向无效。");
                var id = slot.ToLowerInvariant();
                var copy = (JsonObject)entry.DeepClone();
                copy.Remove("slot"); copy["id"] = id;
                if (!byId.TryAdd(id, copy)) throw new ArgumentException("旧菜单方向重复。");
            }
            var upgraded = new JsonArray();
            // Preserve all four original positions, including unassigned directions.
            if (byId.Count != 0)
                foreach (var id in new[] { "top", "right", "bottom", "left" })
                    upgraded.Add(byId.GetValueOrDefault(id) ?? new JsonObject { ["id"] = id, ["actionId"] = "" });
            profile["entries"] = upgraded;
        }
        return UpgradeTwo(root);
    }
    private static string UpgradeTwo(JsonObject root)
    {
        if (root["profiles"] is not JsonArray profiles) throw new ArgumentException("缺少菜单列表。");
        if (root.ContainsKey("presets")) throw new ArgumentException("旧配置包含未知的预设字段。");
        root["schemaVersion"] = 3;
        foreach (var node in profiles)
        {
            if (node is not JsonObject profile || profile["schemaVersion"]?.GetValue<int>() != 2
                || profile.ContainsKey("ringCount") || profile.ContainsKey("centerText") || profile.ContainsKey("centerImage") || profile.ContainsKey("centerGlyph") || profile["entries"] is not JsonArray entries)
                throw new ArgumentException("旧菜单结构无效。");
            profile["schemaVersion"] = 3; profile["ringCount"] = 1;
            foreach (var item in entries)
            {
                if (item is not JsonObject entry || entry.ContainsKey("ring") || entry.ContainsKey("image"))
                    throw new ArgumentException("旧按钮结构无效。");
                entry["ring"] = 0;
            }
        }
        return root.ToJsonString();
    }
    public static void ValidateImage(string? image)
    {
        if (image is null) return;
        if (image.Length > 180000) throw new ArgumentException("图标图片过大。");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(image); }
        catch (FormatException) { throw new ArgumentException("图标图片格式无效。"); }
        if (bytes.Length < 24 || !bytes.AsSpan(0,8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}))
            throw new ArgumentException("图标必须是内嵌 PNG 图片。");
        var width = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4));
        var height = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20,4));
        if (width is 0 or > 256 || height is 0 or > 256) throw new ArgumentException("图标尺寸不能超过 256 像素。");
    }
    internal static bool ValidId(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 160
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');
}