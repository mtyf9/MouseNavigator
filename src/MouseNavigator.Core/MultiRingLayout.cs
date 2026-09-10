using MouseNavigator.Contracts;
namespace MouseNavigator.Core;
public sealed class MultiRingLayout(MenuProfile menu)
{
    public const double Band = 112;
    public double Rotation(int ring) => menu.RingRotations?.GetValueOrDefault(ring) ?? 0;
    public double Diameter => 320 + Math.Max(0, menu.RingCount - 1) * Band * 2;
    public double DisplayDiameter => Diameter * menu.SizeScale;
    public double Inner(int ring) => ring == 0 ? RingGeometry.DeadZone : 151 + (ring - 1) * Band + 12;
    public double Outer(int ring) => 151 + ring * Band;
    // Preserve unrotated layouts. Once a ring has been rotated, count changes snap
    // backwards to the nearest orientation with a cardinal sector boundary.
    public static double SnapRotation(double rotation, int count)
    {
        if (count == 0) return 0;
        rotation = (rotation % 360 + 360) % 360;
        var best = double.NegativeInfinity;
        for (var i = 0; i < count; i++)
            for (var cardinal = 0; cardinal < 4; cardinal++)
            {
                var candidate = ((90 + cardinal * 90 - (i + 0.5) * 360 / count) % 360 + 360) % 360;
                if (candidate > rotation + 1e-8) candidate -= 360;
                best = Math.Max(best, candidate);
            }
        return (best % 360 + 360) % 360;
    }
    public static MenuProfile AlignChangedCounts(MenuProfile original, MenuProfile updated)
    {
        if (original.RingRotations is null) return updated;
        var rotations = updated.RingRotations?.ToDictionary(p => p.Key, p => p.Value) ?? [];
        foreach (var pair in original.RingRotations)
        {
            if (pair.Key >= updated.RingCount) continue;
            var count = updated.Entries.Count(e => e.Ring == pair.Key);
            if (count != original.Entries.Count(e => e.Ring == pair.Key))
                rotations[pair.Key] = SnapRotation(pair.Value, count);
        }
        return updated with { RingRotations = rotations };
    }
    public int? RingAt(double x, double y)
    {
        var r = Math.Sqrt(x*x+y*y);
        if (r < RingGeometry.DeadZone || r > Outer(menu.RingCount - 1) || menu.RingCount == 0) return null;
        var ring = r <= 151 ? 0 : (int)Math.Ceiling((r - 151) / Band);
        return ring < menu.RingCount && r >= Inner(ring) ? ring : null;
    }
    public string? HitTest(double x, double y, bool includeGaps = false)
    {
        if (RingAt(x,y) is not int ring) return null;
        var entries = menu.Entries.Where(e => e.Ring == ring).ToArray();
        if (entries.Length == 0) return null;
        var angle = ((Math.Atan2(y,x)*180/Math.PI + 450 - Rotation(ring))%360+360)%360;
        var step = 360d / entries.Length;
        var index = (int)Math.Floor((angle + step/2)/step)%entries.Length;
        var distance = Math.Abs((angle-index*step+540)%360-180);
        return includeGaps || distance <= step/2-RingGeometry.GapDegrees ? entries[index].Id : null;
    }
}
/// <summary>A drag previews from the original snapshot, never incrementally mutating the draft.</summary>
public sealed class MenuDragSession
{
    public MenuProfile Original { get; }
    public string? SourceId { get; }
    public ButtonPreset? Preset { get; }
    public string NewButtonId { get; } = "button-" + Guid.NewGuid().ToString("N");
    public MenuDragSession(MenuProfile original, string? sourceId, ButtonPreset? preset = null)
    { Original=original; SourceId=sourceId; Preset=preset; }
    // Hit a destination slot in the final arrangement. Using this fixed grid avoids
    // feedback loops when the animated neighbours move underneath the pointer.
    public int InsertionIndex(int ring, double x, double y)
    {
        var count = Original.Entries.Count(e => e.Ring == ring && e.Id != SourceId) + 1;
        var rotation = new MultiRingLayout(Original).Rotation(ring);
        if (Original.RingRotations?.ContainsKey(ring) == true && count != Original.Entries.Count(e => e.Ring == ring))
            rotation = MultiRingLayout.SnapRotation(rotation, count);
        var angle = ((Math.Atan2(y, x) * 180 / Math.PI + 450 - rotation) % 360 + 360) % 360;
        return (int)Math.Floor((angle + 180d / count) / (360d / count)) % count;
    }
    public MenuProfile Preview(int ring, string? targetId)
    {
        var group = Original.Entries.Where(e => e.Ring == ring).ToList();
        var index = group.FindIndex(e => e.Id == targetId);
        return PreviewAt(ring, index < 0 ? group.Count : index);
    }
    public MenuProfile PreviewAt(int ring, int index)
    {
        if (ring < 0 || ring >= Original.RingCount) throw new ArgumentException("请拖到一个圈内。");
        var entries = Original.Entries.ToList();
        MenuEntry added;
        if (SourceId is not null)
        {
            var source = entries.SingleOrDefault(e => e.Id == SourceId) ?? throw new ArgumentException("源按钮不存在。");
            entries.Remove(source);
            added = source with { Ring = ring };
        }
        else if (Preset is not null)
            added = new(NewButtonId, Preset.ActionId, Preset.Name, Preset.Glyph, ring, Preset.Image);
        else throw new ArgumentException("没有可拖动的按钮。");
        var group = entries.Where(e => e.Ring == ring).ToList();
        if (group.Count >= RingGeometry.MaximumButtons) throw new ArgumentException("此圈已满，请添加外圈。");
        index = Math.Clamp(index, 0, group.Count);
        // Only the destination ring's order changes; unrelated rings retain their entries.
        var position = index < group.Count ? entries.IndexOf(group[index])
            : group.Count > 0 ? entries.IndexOf(group[^1]) + 1 : entries.Count;
        entries.Insert(position, added);
        return MultiRingLayout.AlignChangedCounts(Original, Original with { Entries = entries.ToArray() });
    }
}