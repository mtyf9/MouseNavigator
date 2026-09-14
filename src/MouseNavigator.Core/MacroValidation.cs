using MouseNavigator.Contracts;
namespace MouseNavigator.Core;
public static class MacroValidation
{
    public static void Validate(MacroDefinition macro)
    {
        if(string.IsNullOrWhiteSpace(macro.Name)||macro.Name.Length>80)throw new ArgumentException("请输入宏名称（最多 80 字）。");
        if(macro.Steps is null||macro.Steps.Count is <1 or >100)throw new ArgumentException("宏需要 1～100 个步骤。");
        long duration=0;
        foreach(var step in macro.Steps)
        {
            if(step is null||step.Keys is null)throw new ArgumentException("宏步骤无效。");
            ShortcutKeys.Validate(step.Keys);
            if(step.DelayMilliseconds is <0 or >60000)throw new ArgumentException("每步延时应为 0～60000 毫秒。");
            duration+=step.DelayMilliseconds;
        }
        if(duration>300000)throw new ArgumentException("宏的总延时不能超过 5 分钟。");
    }
}