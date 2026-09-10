namespace MouseNavigator.Core;
/// <summary>Tracks physical key transitions instead of querying asynchronous key state inside a hook.</summary>
public sealed class ShortcutRecording
{
    private readonly HashSet<ushort> held=[];
    private ushort[]? chord;
    private bool canceled;
    public bool Completed {get;private set;}
    public ushort[]? Keys=>chord;
    public void Accept(ushort key,bool down)
    {
        if(Completed)return;
        if(down)
        {
            if(!held.Add(key))return;
            var normalized=Normalize(key);
            if(normalized==27&&held.Count==1)canceled=true;
            else if(chord is null&&!canceled&&ShortcutKeys.MainKeys.ContainsKey(normalized))
                chord=new ushort[]{17,18,16,91}.Where(m=>held.Any(k=>Normalize(k)==m)).Append(normalized).ToArray();
        }
        else held.Remove(key);
        if(held.Count==0&&(canceled||chord is not null))Completed=true;
    }
    private static ushort Normalize(ushort key)=>key switch{160 or 161=>16,162 or 163=>17,164 or 165=>18,92=>91,_=>key};
}