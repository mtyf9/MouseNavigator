namespace MouseNavigator.Core;

public readonly record struct WindowIdentity(nint Handle, uint ProcessId);
public sealed record WindowCandidate(WindowIdentity Identity, string Title);
