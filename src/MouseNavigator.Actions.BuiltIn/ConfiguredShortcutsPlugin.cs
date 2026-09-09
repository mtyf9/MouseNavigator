using MouseNavigator.Contracts;

namespace MouseNavigator.Actions.BuiltIn;

/// <summary>User documents provide data; execution still goes through the platform boundary.</summary>
public sealed class ConfiguredShortcutsPlugin(IReadOnlyList<ShortcutDefinition> definitions) : INavigatorPlugin
{
    public string Id => "shortcuts";
    public int ApiVersion => 1;
    public IReadOnlyList<INavigatorAction> CreateActions(IPlatformActions platform) =>
        definitions.Select(d => (INavigatorAction)new ShortcutAction(d, platform)).ToArray();

    private sealed class ShortcutAction(ShortcutDefinition definition, IPlatformActions platform) : INavigatorAction
    {
        private readonly ushort[] keys = definition.Keys.ToArray();
        public ActionDescriptor Descriptor { get; } = new(definition.Id, definition.Name, "\uE765", "自定义键盘快捷键");
        public ValueTask<ActionResult> ExecuteAsync(ApplicationContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(platform.SendShortcut(context.WindowHandle, keys));
        }
    }
}