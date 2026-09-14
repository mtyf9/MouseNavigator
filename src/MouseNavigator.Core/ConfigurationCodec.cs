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
        var folders=configuration.PresetFolders??[];
        if(folders.Count>100||folders.Any(f=>f is null||!ValidId(f.Id)||string.IsNullOrWhiteSpace(f.Name)||f.Name.Length>80)||folders.Select(f=>f.Id).Distinct().Count()!=folders.Count||folders.GroupBy(f=>f.ParentId).Any(g=>g.Select(f=>f.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=g.Count()))throw new ArgumentException("预设文件夹无效或重复。");
        var folderMap=folders.ToDictionary(f=>f.Id);
        foreach(var folder in folders)
        {
            var seen=new HashSet<string>();string? current=folder.Id;
            while(current is not null)
            {
                if(!seen.Add(current)||!folderMap.TryGetValue(current,out var node))throw new ArgumentException("文件夹父级无效或形成循环。");
                current=node.ParentId;
            }
        }
        if(configuration.PresetOrder is {} order && (order.Count>10000 || order.Any(id=>!ValidId(id)) || order.Distinct().Count()!=order.Count))throw new ArgumentException("预设排序无效。");
        var presets = configuration.Presets ?? [];
        var presetIds = new HashSet<string>();
        foreach (var preset in presets)
        {
            if (preset is null || !ValidId(preset.Id) || !presetIds.Add(preset.Id) || string.IsNullOrWhiteSpace(preset.Name)
                || preset.Name.Length > 80 || (preset.ActionId is null || (preset.ActionId.Length > 0 && !ValidId(preset.ActionId))) || preset.Glyph?.Length > 8)
                throw new ArgumentException("预设按钮无效。");
            if(preset.FolderId is not null&&!folders.Any(f=>f.Id==preset.FolderId))throw new ArgumentException("预设按钮引用了不存在的文件夹。");
            if (preset.Keys is not null) ShortcutKeys.Validate(preset.Keys);
            else if (preset.ActionId.StartsWith("shortcuts.")) throw new ArgumentException("快捷键预设缺少按键。");
            ValidateImage(preset.Image);ValidateLaunch(preset.Launch);if(preset.Macro is not null)MacroValidation.Validate(preset.Macro);
        }
        foreach (var profile in configuration.Profiles)
        {
            ValidatePreviewAppearance(profile.PreviewAppearance);
            if (profile.CenterText?.Length > 40) throw new ArgumentException("中心文字不能超过 40 个字符。");
            if (profile.CenterGlyph?.Length > 8) throw new ArgumentException("中心图标无效。");
            ValidateImage(profile.CenterImage);
            if(profile.AccentColor is {} color&&(color.Length!=7||color[0]!='#'||!color[1..].All(Uri.IsHexDigit)))throw new ArgumentException("菜单颜色需要为 #RRGGBB。");
            if(profile.NormalColor is {} normal&&(normal.Length!=7||normal[0]!='#'||!normal[1..].All(Uri.IsHexDigit)))throw new ArgumentException("非触发颜色需要为 #RRGGBB。");
            if(!double.IsFinite(profile.ActiveOpacity)||profile.ActiveOpacity is <0 or >1||!double.IsFinite(profile.NormalOpacity)||profile.NormalOpacity is <0 or >1||!double.IsFinite(profile.ButtonGap)||profile.ButtonGap is <0 or >20)throw new ArgumentException("透明度或按钮间隙无效。");
            if (!double.IsFinite(profile.SizeScale) || profile.SizeScale is < 0.5 or > 2) throw new ArgumentException("悬浮窗大小必须为 50% 到 200%。");
            if(profile.RingRotations is not null && profile.RingRotations.Any(pair=>pair.Key<0||pair.Key>=profile.RingCount||!double.IsFinite(pair.Value)||pair.Value<0||pair.Value>=360))
                throw new ArgumentException("圈旋转角度无效。");
        }
        foreach (var entry in configuration.Profiles.SelectMany(p => p.Entries))
        {
            ValidateImage(entry.Image);ValidateLaunch(entry.Launch);if(entry.Macro is not null)MacroValidation.Validate(entry.Macro);
            if (entry.ActionId.Length == 0) continue;
            if (!ValidId(entry.ActionId)) throw new ArgumentException("动作标识无效。");
            if (entry.ActionId.StartsWith("shortcuts.", StringComparison.Ordinal) && !ids.Contains(entry.ActionId))
                throw new ArgumentException($"缺少快捷键定义：{entry.ActionId}");
        }
    }
    public static void ValidatePreviewAppearance(WindowPreviewAppearance? value)
    {
        if(value is null)return;
        foreach(var color in new[]{value.BackgroundColor,value.CardColor,value.HighlightColor,value.TextColor})
            if(color is not null&&(color.Length!=7||color[0]!='#'||!color[1..].All(Uri.IsHexDigit)))throw new ArgumentException("窗口预览颜色无效。");
        if(!double.IsFinite(value.BackgroundOpacity)||value.BackgroundOpacity is <0 or >1||!double.IsFinite(value.CardOpacity)||value.CardOpacity is <0 or >1||!double.IsFinite(value.CornerRadius)||value.CornerRadius is <0 or >30)throw new ArgumentException("窗口预览透明度或圆角无效。");
    }
    public static void ValidateLaunch(ApplicationLaunch? launch)
    {
        if(launch is null)return;
        if(string.IsNullOrWhiteSpace(launch.ExecutablePath)||launch.ExecutablePath.Length>1024
            ||!Path.IsPathFullyQualified(launch.ExecutablePath)||launch.ExecutablePath.IndexOfAny(['"','\0','\r','\n'])>=0
            ||!Path.GetExtension(launch.ExecutablePath).Equals(".exe",StringComparison.OrdinalIgnoreCase)
            ||launch.Arguments is null||launch.Arguments.Length>4096||launch.Arguments.Contains('\0'))
            throw new ArgumentException("请选择完整的 .exe 程序路径，启动参数不能超过 4096 个字符。");
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