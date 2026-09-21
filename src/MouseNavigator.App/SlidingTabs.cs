using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
namespace MouseNavigator.App;

/// <summary>Text tabs with one sliding accent underline and native radio keyboard semantics.</summary>
internal sealed class SlidingTabs : UserControl
{
    private readonly StackPanel headers=new(){Orientation=Orientation.Horizontal,Spacing=12};
    private readonly Border line=new(){Height=2,CornerRadius=new(1)};
    private readonly Canvas rail=new(){Height=3,HorizontalAlignment=HorizontalAlignment.Stretch};
    private readonly List<RadioButton> items=[];
    private readonly SurfaceMotion motion=new();
    private int selected;
    public event Action<int>? SelectionChanged;
    public int SelectedIndex {get=>selected;set=>Select(value);}
    public SlidingTabs(params string[] labels)
    {
        var root=new StackPanel{Spacing=1,HorizontalAlignment=HorizontalAlignment.Left};
        line.Background=(Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"];
        rail.Children.Add(line);root.Children.Add(headers);root.Children.Add(rail);Content=root;
        var group=Guid.NewGuid().ToString();
        var template=(ControlTemplate)XamlReader.Load("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="RadioButton">
                <ContentPresenter Content="{TemplateBinding Content}" Padding="{TemplateBinding Padding}" HorizontalAlignment="Left"/>
            </ControlTemplate>
            """);
        for(var i=0;i<labels.Length;i++)
        {
            var index=i;
            var item=new RadioButton{Content=labels[i],GroupName=group,Template=template,MinWidth=0,MinHeight=32,Padding=new(0,6,0,6),UseSystemFocusVisuals=true};
            item.Checked+=(_,_)=>Select(index);headers.Children.Add(item);items.Add(item);
        }
        Loaded+=(_,_)=>Position(false);SizeChanged+=(_,_)=>Position(false);Unloaded+=(_,_)=>motion.Stop();
        Select(0);
    }
    private void Select(int index)
    {
        index=Math.Clamp(index,0,items.Count-1);if(selected==index&&items[index].IsChecked==true)return;var changed=selected!=index;selected=index;
        for(var i=0;i<items.Count;i++){items[i].IsChecked=i==index;items[i].Opacity=i==index?1:.65;}
        Position(changed);if(changed)SelectionChanged?.Invoke(index);
    }
    private void Position(bool animate)
    {
        if(!IsLoaded||items.Count==0)return;
        var x=items[selected].TransformToVisual(headers).TransformPoint(new(0,0)).X;
        var width=items[selected].ActualWidth;
        var from=Canvas.GetLeft(line);if(double.IsNaN(from))from=x;
        var oldWidth=double.IsNaN(line.Width)?width:line.Width;
        motion.Start(animate?180:0,t=>{Canvas.SetLeft(line,from+(x-from)*t);line.Width=Math.Max(0,oldWidth+(width-oldWidth)*t);});
    }
}
