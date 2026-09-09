using MouseNavigator.Contracts;

namespace MouseNavigator.Core;

public readonly record struct WindowIdentity(nint Handle, uint ProcessId);
public sealed record WindowCandidate(WindowIdentity Identity, string Title);

/// <summary>Keep first-seen order across foreground/Z-order changes. Prune closed windows, append new ones.</summary>
public sealed class WindowCycle
{
    private readonly List<WindowIdentity> order = [];

    public WindowCandidate? Select(IReadOnlyList<WindowCandidate> windows, nint source, WindowDirection direction)
    {
        if (direction is not (WindowDirection.Previous or WindowDirection.Next)) throw new ArgumentOutOfRangeException(nameof(direction));
        var available = windows.DistinctBy(w => w.Identity).ToDictionary(w => w.Identity);
        order.RemoveAll(id => !available.ContainsKey(id));
        foreach (var window in windows)
            if (!order.Contains(window.Identity)) order.Add(window.Identity);
        if (order.Count == 0) return null;
        var index = order.FindIndex(id => id.Handle == source);
        if (order.Count == 1 && index == 0) return null;
        var next = index < 0 ? (direction == WindowDirection.Next ? 0 : order.Count - 1)
            : (index + (int)direction + order.Count) % order.Count;
        return available[order[next]];
    }
}
