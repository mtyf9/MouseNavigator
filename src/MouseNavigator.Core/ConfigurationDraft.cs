using MouseNavigator.Contracts;

namespace MouseNavigator.Core;

/// <summary>Edits a detached working copy; callers explicitly validate/save/apply a snapshot.</summary>
public sealed class ConfigurationDraft
{
    private readonly List<MenuProfile> profiles;
    private readonly List<ShortcutDefinition> shortcuts;
    public IReadOnlyList<MenuProfile> Profiles => profiles;
    public ConfigurationDraft(NavigatorConfiguration configuration)
    {
        var copy = ConfigurationCodec.Deserialize(ConfigurationCodec.Serialize(configuration));
        profiles = copy.Profiles.ToList();
        shortcuts = copy.Shortcuts.ToList();
    }
    public MenuProfile Find(string id) => profiles.Single(p => p.Id == id);
    public ShortcutDefinition? Shortcut(string? actionId) => shortcuts.FirstOrDefault(s => s.Id == actionId);
    public void Update(string id, string name, IReadOnlyList<ApplicationMatch> applications, int priority)
    {
        var current = Find(id);
        if (current.Applications.Count == 0 && applications.Count != 0)
            throw new ArgumentException("全局菜单不能绑定到单个应用。");
        if (current.Applications.Count != 0 && applications.Count == 0)
            throw new ArgumentException("应用菜单需要至少一个进程名。");
        profiles[profiles.IndexOf(current)] = current with { Name = name, Applications = applications.ToArray(), Priority = priority };
    }
    public string Add()
    {
        var global = profiles.Single(p => p.Applications.Count == 0);
        var id = "app-" + Guid.NewGuid().ToString("N");
        profiles.Add(global with { Id = id, Name = "新应用菜单", Applications = [new("app.exe")], Priority = 10, Entries = global.Entries.ToArray() });
        return id;
    }
    public void Delete(string id)
    {
        var profile = Find(id);
        if (profile.Applications.Count == 0) throw new ArgumentException("全局菜单必须保留。");
        profiles.Remove(profile);
        PruneShortcuts();
    }
    public void SetAction(string id, RingSlot slot, string? actionId)
    {
        var profile = Find(id);
        var entries = profile.Entries.Where(e => e.Slot != slot).ToList();
        if (actionId is not null) entries.Add(new(slot, actionId));
        profiles[profiles.IndexOf(profile)] = profile with { Entries = entries.OrderBy(e => e.Slot).ToArray() };
        PruneShortcuts();
    }
    public void SetShortcut(string id, RingSlot slot, string name, IReadOnlyList<ushort> keys)
    {
        var actionId = "shortcuts." + Guid.NewGuid().ToString("N");
        shortcuts.Add(new(actionId, name, keys.ToArray()));
        SetAction(id, slot, actionId);
    }
    public NavigatorConfiguration Snapshot()
    {
        var result = new NavigatorConfiguration(1, profiles.ToArray(), shortcuts.ToArray());
        ConfigurationCodec.Validate(result);
        return result;
    }
    private void PruneShortcuts()
    {
        var referenced = profiles.SelectMany(p => p.Entries).Select(e => e.ActionId).ToHashSet(StringComparer.Ordinal);
        shortcuts.RemoveAll(s => !referenced.Contains(s.Id));
    }
}