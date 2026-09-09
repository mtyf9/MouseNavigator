namespace MouseNavigator.Contracts;

public sealed record ApplicationContext(nint WindowHandle, string ProcessName, string WindowTitle);
public sealed record ActionResult(bool Succeeded, string Message)
{
    public static ActionResult Success(string message) => new(true, message);
    public static ActionResult Failure(string message) => new(false, message);
}

public enum ActionInteraction { Invoke, WindowPreview }
public sealed record ActionDescriptor(string Id, string Name, string Glyph, string Description,
    ActionInteraction Interaction = ActionInteraction.Invoke);

/// <summary>Stable action IDs belong to a plugin namespace, e.g. windows.window.preview.</summary>
public interface INavigatorAction
{
    ActionDescriptor Descriptor { get; }
    ValueTask<ActionResult> ExecuteAsync(ApplicationContext context, CancellationToken cancellationToken);
}

/// <summary>Explicit composition boundary. Menu documents only reference action IDs, never assemblies.</summary>
public interface INavigatorPlugin
{
    string Id { get; }
    int ApiVersion { get; }
    IReadOnlyList<INavigatorAction> CreateActions(IPlatformActions platform);
}

public interface IPlatformActions
{
    ActionResult SendShortcut(nint source, params ushort[] keys);
}

public enum RingSlot { Top, Right, Bottom, Left }
public sealed record MenuEntry(RingSlot Slot, string ActionId);
public sealed record ApplicationMatch(string ProcessName);
public sealed record MenuProfile(int SchemaVersion, string Id, string Name,
    IReadOnlyList<ApplicationMatch> Applications, IReadOnlyList<MenuEntry> Entries, int Priority = 0);
