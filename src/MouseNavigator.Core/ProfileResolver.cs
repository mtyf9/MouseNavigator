using MouseNavigator.Contracts;

namespace MouseNavigator.Core;

public sealed class ProfileResolver
{
    private readonly MenuProfile[] profiles;
    private readonly MenuProfile fallback;
    public IReadOnlyList<MenuProfile> Profiles => profiles;

    public ProfileResolver(IEnumerable<MenuProfile> profiles)
    {
        var supplied = profiles.ToArray();
        foreach (var profile in supplied)
        {
            if (profile is null || profile.SchemaVersion != 3 || !ConfigurationCodec.ValidId(profile.Id)
                || string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Length > 80
                || profile.Applications is null || profile.Applications.Count > 30
                || profile.Applications.Any(a => a is null || string.IsNullOrWhiteSpace(a.ProcessName)
                    || a.ProcessName.Length > 120 || a.ProcessName.IndexOfAny(['/', '\\', '*', '?', ':']) >= 0
                    || string.IsNullOrWhiteSpace(Normalize(a.ProcessName)))
                || profile.Entries is null || profile.RingCount is < 0 or > 10000 || profile.Entries.GroupBy(e => e?.Ring).Any(g => g.Count() > RingGeometry.MaximumButtons)
                || profile.Entries.Any(e => e is null || !ConfigurationCodec.ValidId(e.Id) || e.ActionId is null || e.Ring < 0 || e.Ring >= profile.RingCount || e.Label?.Length > 80 || e.Glyph?.Length > 8)
                || profile.Entries.Select(e => e.Id).Distinct().Count() != profile.Entries.Count)
                throw new ArgumentException("菜单结构无效：请检查名称、应用进程名和按钮设置。");
        }
        this.profiles = supplied.OrderByDescending(p => p.Priority).ThenBy(p => p.Id, StringComparer.Ordinal).ToArray();
        if (this.profiles.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != this.profiles.Length)
            throw new ArgumentException("菜单标识重复。");
        var defaults = this.profiles.Where(p => p.IsDefault).ToArray();
        if (defaults.Length != 1) throw new ArgumentException("必须有且只有一个全局菜单。");
        fallback = defaults[0];
    }

    public MenuProfile Resolve(ApplicationContext context) => profiles.FirstOrDefault(p =>
        p.Enabled && !p.IsDefault && p.Applications.Any(a => Normalize(a.ProcessName).Equals(Normalize(context.ProcessName),
            StringComparison.OrdinalIgnoreCase))) ?? fallback;

    private static string Normalize(string name)
    {
        name = name.Trim();
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }
}