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


public sealed record MenuEntry(string Id, string ActionId, string? Label = null, string? Glyph = null, int Ring = 0, string? Image = null);
public sealed record ApplicationMatch(string ProcessName);
public sealed record MenuProfile(int SchemaVersion, string Id, string Name,
    IReadOnlyList<ApplicationMatch> Applications, IReadOnlyList<MenuEntry> Entries, int Priority = 0, int RingCount = 1, string? CenterText = null, string? CenterImage = null, string? CenterGlyph = null,
    bool Enabled = true, bool? IsGlobalDefault = null, IReadOnlyDictionary<int,double>? RingRotations = null, double SizeScale = 1)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsDefault => IsGlobalDefault ?? Applications.Count == 0;
}
