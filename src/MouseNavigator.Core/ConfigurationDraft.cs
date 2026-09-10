using MouseNavigator.Contracts;

namespace MouseNavigator.Core;

/// <summary>Edits a detached working copy; callers explicitly validate/save/apply a snapshot.</summary>
public sealed class ConfigurationDraft
{
    private readonly List<MenuProfile> profiles;
    private readonly List<ShortcutDefinition> shortcuts;
    private readonly List<ButtonPreset> presets;
    public IReadOnlyList<ButtonPreset> Presets => presets;
    public IReadOnlyList<MenuProfile> Profiles => profiles;
    public ConfigurationDraft(NavigatorConfiguration configuration)
    {
        var copy = ConfigurationCodec.Deserialize(ConfigurationCodec.Serialize(configuration));
        profiles = copy.Profiles.ToList();
        shortcuts = copy.Shortcuts.ToList(); presets = (copy.Presets ?? []).ToList();
    }
    public MenuProfile Find(string id) => profiles.Single(p => p.Id == id);
    public ShortcutDefinition? Shortcut(string? actionId) => shortcuts.FirstOrDefault(s => s.Id == actionId);
    public void Update(string id, string name, IReadOnlyList<ApplicationMatch> applications, int priority)
    {
        var current = Find(id);
        profiles[profiles.IndexOf(current)] = current with { Name = name, Applications = applications.ToArray(), Priority = priority };
    }
    public string Add()
    {
        var global = profiles.Single(p => p.IsDefault);
        var id = "app-" + Guid.NewGuid().ToString("N");
        profiles.Add(global with { Id = id, Name = "新悬浮菜单", Applications = [], IsGlobalDefault = false, Enabled = true, Priority = 10, Entries = global.Entries.ToArray() });
        return id;
    }
    public void SetEnabled(string id,bool enabled)
    {
        var p=Find(id);profiles[profiles.IndexOf(p)]=p with{Enabled=enabled};
    }
    public void SetDefault(string id)
    {
        _=Find(id);
        for(var i=0;i<profiles.Count;i++)profiles[i]=profiles[i] with{IsGlobalDefault=profiles[i].Id==id};
    }
    public void SetSize(string id, double scale)
    {
        if (!double.IsFinite(scale) || scale is < 0.5 or > 2) throw new ArgumentException("悬浮窗大小必须为 50% 到 200%。");
        var p = Find(id); profiles[profiles.IndexOf(p)] = p with { SizeScale = scale };
    }
    public void RotateRing(string id,int ring,int direction)
    {
        var p=Find(id);if(ring<0||ring>=p.RingCount)throw new ArgumentException("请选择圈。");
        var count=p.Entries.Count(e=>e.Ring==ring);if(count==0)return;
        var rotations=p.RingRotations?.ToDictionary(e=>e.Key,e=>e.Value)??[];
        rotations[ring]=((rotations.GetValueOrDefault(ring)+Math.Sign(direction)*180d/count)%360+360)%360;
        profiles[profiles.IndexOf(p)]=p with{RingRotations=rotations};
    }
    public MenuDocument ExportMenu(string id)
    {
        var p=Find(id);var used=p.Entries.Select(e=>e.ActionId).ToHashSet();
        return new(1,p,shortcuts.Where(s=>used.Contains(s.Id)).ToArray());
    }
    public string ImportMenu(MenuDocument document,string? overwriteId=null)
    {
        MenuDocumentCodec.Validate(document);
        var existing=overwriteId is null?null:Find(overwriteId);
        var name=existing?.Name??document.Menu.Name;var suffix=1;
        while(existing is null&&profiles.Any(p=>p.Name.Equals(name,StringComparison.OrdinalIgnoreCase)))
        {
            var tail="+"+suffix++;name=document.Menu.Name[..Math.Min(document.Menu.Name.Length,80-tail.Length)]+tail;
        }
        var remap=document.Shortcuts.ToDictionary(s=>s.Id,s=>"shortcuts."+Guid.NewGuid().ToString("N"));
        var imported=document.Menu with{Id=existing?.Id??"app-"+Guid.NewGuid().ToString("N"),Name=name,
            IsGlobalDefault=existing?.IsDefault??false,
            Entries=document.Menu.Entries.Select(e=>remap.TryGetValue(e.ActionId,out var action)?e with{ActionId=action}:e).ToArray()};
        var next=profiles.Where(p=>p.Id!=imported.Id).Append(imported).ToArray();
        var referenced=next.SelectMany(p=>p.Entries).Select(e=>e.ActionId).ToHashSet();
        var definitions=shortcuts.Concat(document.Shortcuts.Select(s=>s with{Id=remap[s.Id]})).Where(s=>referenced.Contains(s.Id)).ToArray();
        ConfigurationCodec.Validate(new(3,next,definitions,presets));
        if(existing is null)profiles.Add(imported);else profiles[profiles.IndexOf(existing)]=imported;
        shortcuts.Clear();shortcuts.AddRange(definitions);PruneShortcuts();return imported.Id;
    }
    public void Delete(string id)
    {
        var profile = Find(id);
        if (profile.IsDefault) throw new ArgumentException("全局菜单必须保留。");
        profiles.Remove(profile);
        PruneShortcuts();
    }
    public string AddButton(string id, int ring = 0)
    {
        var profile = Find(id);
        if (ring < 0 || ring >= profile.RingCount) throw new ArgumentException("请选择一个圈。");
        if (profile.Entries.Count(e => e.Ring == ring) >= RingGeometry.MaximumButtons) throw new ArgumentException("每圈最多支持 12 个按钮。");
        var buttonId = "button-" + Guid.NewGuid().ToString("N");
        ReplaceEntries(profile, profile.Entries.Append(new MenuEntry(buttonId, "", Ring: ring)));
        return buttonId;
    }
    public void RemoveButton(string id, string buttonId)
    {
        var profile = Find(id);
        ReplaceEntries(profile, profile.Entries.Where(e => e.Id != buttonId));
        PruneShortcuts();
    }
    public void SetAppearance(string id, string buttonId, string? label, string? glyph, string? image = null)
    {
        var profile = Find(id);
        ReplaceEntries(profile, profile.Entries.Select(e => e.Id == buttonId ? e with {
            Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
            Glyph = string.IsNullOrWhiteSpace(glyph) ? null : glyph, Image = image } : e));
    }
    public void SetCenterAppearance(string id, string? text, string? image, string? glyph = null)
    {
        if (glyph?.Length > 8) throw new ArgumentException("中心图标无效。");
        if (text?.Length > 40) throw new ArgumentException("中心文字不能超过 40 个字符。");
        ConfigurationCodec.ValidateImage(image);
        var profile = Find(id);
        profiles[profiles.IndexOf(profile)] = profile with { CenterText = text?.Trim(), CenterImage = image, CenterGlyph = string.IsNullOrEmpty(glyph) ? null : glyph };
    }
    public void SetAction(string id, string buttonId, string? actionId)
    {
        var profile = Find(id);
        var entries = profile.Entries.ToList();
        var index = entries.FindIndex(e => e.Id == buttonId);
        if (index < 0) throw new ArgumentException("按钮不存在。");
        entries[index] = entries[index] with { ActionId = actionId ?? "" };
        ReplaceEntries(profile, entries);
        PruneShortcuts();
    }
    public void SetShortcut(string id, string buttonId, string name, IReadOnlyList<ushort> keys)
    {
        if (!Find(id).Entries.Any(e => e.Id == buttonId)) throw new ArgumentException("按钮不存在。");
        var actionId = "shortcuts." + Guid.NewGuid().ToString("N");
        shortcuts.Add(new(actionId, name, keys.ToArray()));
        SetAction(id, buttonId, actionId);
    }
    public int AddRing(string id, bool inner = false)
    {
        var profile = Find(id);
        if(profile.RingCount>=10000)throw new ArgumentException("当前配置的圈数量过多。");
        profiles[profiles.IndexOf(profile)] = profile with {
            RingCount = checked(profile.RingCount + 1),
            Entries = inner ? profile.Entries.Select(e => e with { Ring = e.Ring + 1 }).ToArray() : profile.Entries,
            RingRotations = inner ? profile.RingRotations?.ToDictionary(p => p.Key + 1, p => p.Value) : profile.RingRotations
        };
        return inner ? 0 : profile.RingCount;
    }
    public void RemoveRing(string id, int ring)
    {
        var profile = Find(id);
        if (ring < 0 || ring >= profile.RingCount) throw new ArgumentException("圈不存在。");
        profiles[profiles.IndexOf(profile)] = profile with { RingCount = profile.RingCount - 1,
            Entries = profile.Entries.Where(e => e.Ring != ring).Select(e => e.Ring > ring ? e with { Ring = e.Ring - 1 } : e).ToArray(),
            RingRotations = profile.RingRotations?.Where(p=>p.Key!=ring).ToDictionary(p=>p.Key>ring?p.Key-1:p.Key,p=>p.Value) };
        PruneShortcuts();
    }
    public ButtonPreset SavePreset(string id, string buttonId, string label, string? glyph)
    {
        var entry = Find(id).Entries.Single(e => e.Id == buttonId);
        if (string.IsNullOrWhiteSpace(label) || label.Length>80) throw new ArgumentException("预设名称无效。");

        var preset = new ButtonPreset("preset-" + Guid.NewGuid().ToString("N"), label, entry.ActionId,
            glyph, entry.Image, Shortcut(entry.ActionId)?.Keys.ToArray());
        presets.Add(preset);
        return preset;
    }
    public void RemovePreset(string id) => presets.RemoveAll(p => p.Id == id);
    public void CommitDrop(string id, MenuProfile proposal, ButtonPreset? preset, string? newButtonId)
    {
        var current = Find(id);
        if (proposal.Id != id || proposal.RingCount != current.RingCount) throw new ArgumentException("菜单已变化，请重新拖动。");
        proposal = MultiRingLayout.AlignChangedCounts(current, proposal);
        var nextShortcuts=shortcuts.ToList();
        if(preset?.Keys is not null)
        {
            ShortcutKeys.Validate(preset.Keys);
            if(newButtonId is null||!proposal.Entries.Any(e=>e.Id==newButtonId))throw new ArgumentException("拖入按钮不存在。");
            var actionId="shortcuts."+Guid.NewGuid().ToString("N");
            nextShortcuts.Add(new(actionId,preset.Name,preset.Keys.ToArray()));
            proposal=proposal with {Entries=proposal.Entries.Select(e=>e.Id==newButtonId?e with{ActionId=actionId}:e).ToArray()};
        }
        var candidates=profiles.Select(p=>p.Id==id?proposal:p).ToArray();
        ConfigurationCodec.Validate(new(3,candidates,nextShortcuts,presets));
        profiles[profiles.IndexOf(current)]=proposal;
        shortcuts.Clear();shortcuts.AddRange(nextShortcuts);PruneShortcuts();
    }
    private void ReplaceEntries(MenuProfile profile, IEnumerable<MenuEntry> entries) =>
        profiles[profiles.IndexOf(profile)] = MultiRingLayout.AlignChangedCounts(profile, profile with { Entries = entries.ToArray() });
    public NavigatorConfiguration Snapshot()
    {
        var result = new NavigatorConfiguration(3, profiles.ToArray(), shortcuts.ToArray(), presets.ToArray());
        ConfigurationCodec.Validate(result);
        return result;
    }
    private void PruneShortcuts()
    {
        var referenced = profiles.SelectMany(p => p.Entries).Select(e => e.ActionId).ToHashSet(StringComparer.Ordinal);
        shortcuts.RemoveAll(s => !referenced.Contains(s.Id));
    }
}