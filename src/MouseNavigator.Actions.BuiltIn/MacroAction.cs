using MouseNavigator.Contracts;
using MouseNavigator.Core;
namespace MouseNavigator.Actions.BuiltIn;
internal sealed class MacroAction(IPlatformActions platform):INavigatorAction
{
    public ActionDescriptor Descriptor=>new("windows.macro","宏操作","\uE713","按顺序执行快捷键和延时",Category:"宏操作");
    public async ValueTask<ActionResult> ExecuteAsync(ApplicationContext context,CancellationToken cancellationToken)
    {
        if(context.Macro is not {} macro)return ActionResult.Failure("请先配置宏步骤。");
        MacroValidation.Validate(macro);
        foreach(var step in macro.Steps)
        {
            await Task.Delay(step.DelayMilliseconds,cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if(!platform.IsMacroTargetActive(context.WindowHandle))return ActionResult.Failure("目标应用已失去焦点，宏已停止。");
            var result=platform.SendShortcut(context.WindowHandle,step.Keys.ToArray());
            if(!result.Succeeded)return result;
        }
        return ActionResult.Success($"宏“{macro.Name}”已完成。");
    }
}