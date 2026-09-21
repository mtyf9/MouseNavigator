using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using MouseNavigator.Contracts;

namespace MouseNavigator.App;

/// <summary>One cancellable clock per surface, including native thumbnail opacity.</summary>
internal sealed class SurfaceMotion
{
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(16)};
    private Action<double>? frame;
    private long started;
    private int duration;
    public SurfaceMotion()=>timer.Tick+=(_,_)=>Tick();
    private static readonly global::Windows.UI.ViewManagement.UISettings settings=new(); public static bool Enabled=>settings.AnimationsEnabled;
    public void Start(int milliseconds,Action<double> render)
    {
        Stop();frame=render;
        if(milliseconds<=0||!Enabled){Stop();return;}
        duration=milliseconds;started=Stopwatch.GetTimestamp();render(0);timer.Start();
    }
    private void Tick()
    {
        var t=Math.Clamp(Stopwatch.GetElapsedTime(started).TotalMilliseconds/duration,0,1);
        frame?.Invoke(1-Math.Pow(1-t,3));
        if(t>=1){timer.Stop();frame=null;}
    }
    public void Stop(){timer.Stop();var last=frame;frame=null;last?.Invoke(1);}
    public void Enter(FrameworkElement view,MenuVisualStyle style,double diameter)
    {
        var transform=new CompositeTransform{CenterX=diameter/2,CenterY=diameter/2};
        view.RenderTransform=transform;
        Start(style.Entrance==MenuEntrance.None?0:style.EntranceMilliseconds,t=>
        {
            view.Opacity=t;
            transform.ScaleX=transform.ScaleY=style.Entrance==MenuEntrance.Zoom?.78+.22*t:1;
            transform.TranslateY=style.Entrance==MenuEntrance.Rise?20*(1-t):0;
            transform.Rotation=style.Entrance==MenuEntrance.Turn?-16*(1-t):0;
        });
    }
}

/// <summary>Retarget from the displayed color; repeated pointer samples never restart.</summary>
internal sealed class ColorMotion
{
    private readonly Dictionary<SolidColorBrush,(global::Windows.UI.Color From,global::Windows.UI.Color To,long Start,int Duration)> pending=[];
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(16)};
    public ColorMotion()=>timer.Tick+=(_,_)=>Tick();
    public SolidColorBrush To(Brush? current,SolidColorBrush target,int duration)
    {
        if(current is not SolidColorBrush brush)return target;
        if(pending.TryGetValue(brush,out var old)&&old.To==target.Color)return brush;
        if(!SurfaceMotion.Enabled||duration<=0){pending.Remove(brush);brush.Color=target.Color;return brush;}
        if(brush.Color==target.Color){pending.Remove(brush);return brush;}
        pending[brush]=(brush.Color,target.Color,Stopwatch.GetTimestamp(),duration);timer.Start();return brush;
    }
    private void Tick()
    {
        foreach(var (brush,a) in pending.ToArray())
        {
            var t=Math.Clamp(Stopwatch.GetElapsedTime(a.Start).TotalMilliseconds/a.Duration,0,1);
            byte Mix(byte x,byte y)=>(byte)Math.Round(x+(y-x)*t);
            brush.Color=global::Windows.UI.Color.FromArgb(Mix(a.From.A,a.To.A),Mix(a.From.R,a.To.R),Mix(a.From.G,a.To.G),Mix(a.From.B,a.To.B));
            if(t>=1)pending.Remove(brush);
        }
        if(pending.Count==0)timer.Stop();
    }
    public void Stop(){timer.Stop();foreach(var (b,a) in pending)b.Color=a.To;pending.Clear();}
}
