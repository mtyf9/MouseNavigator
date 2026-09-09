using MouseNavigator.Contracts;

namespace MouseNavigator.Core;

public sealed class ActionRegistry
{
    private readonly Dictionary<string, INavigatorAction> actions = new(StringComparer.Ordinal);
    private readonly HashSet<string> plugins = new(StringComparer.Ordinal);
    public IReadOnlyCollection<INavigatorAction> Actions => actions.Values;
    public void Register(INavigatorPlugin plugin, IPlatformActions platform)
    {
        if (plugin.ApiVersion != 1) throw new ArgumentException("不支持的插件 API 版本。");
        if (plugins.Contains(plugin.Id)) throw new ArgumentException($"插件已注册：{plugin.Id}");
        var pending = plugin.CreateActions(platform).ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var action in pending)
            if (!action.Descriptor.Id.StartsWith(plugin.Id + ".", StringComparison.Ordinal)
                || !ids.Add(action.Descriptor.Id) || actions.ContainsKey(action.Descriptor.Id))
                throw new ArgumentException($"无效或重复的动作标识：{action.Descriptor.Id}");
        foreach (var action in pending) actions.Add(action.Descriptor.Id, action);
        plugins.Add(plugin.Id);
    }

    public INavigatorAction? Find(string id) => actions.GetValueOrDefault(id);

    public async ValueTask<ActionResult> ExecuteAsync(string id, ApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        if (!actions.TryGetValue(id, out var action)) return ActionResult.Failure($"动作不可用：{id}");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await action.ExecuteAsync(context, cancellationToken);
        }
        catch (OperationCanceledException) { return ActionResult.Failure("操作已取消。"); }
        catch (Exception ex) { return ActionResult.Failure($"动作执行失败：{ex.Message}"); }
    }
}
