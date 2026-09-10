using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using MouseNavigator.Core;
using MouseNavigator.Windows;
using Microsoft.UI.Xaml.Controls;
using MouseNavigator.Contracts;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    private async Task ChooseProgramsAsync()
    {
        var chosen=PickProgramsRequested is null?[]:await PickProgramsRequested();if(chosen.Count==0)return;
        var existing=processes.Text.Split([';','；',','],StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
        processes.Text=string.Join("; ",existing.Concat(chosen).Distinct(StringComparer.OrdinalIgnoreCase));UpdateMetadata();
    }
    private void RefreshMatchWarning()
    {
        var p=draft.Find(profileId);
        if(p.IsDefault){matchWarning.Text=p.Enabled?"未匹配专属菜单时使用此菜单。要更换默认菜单，请选择另一个菜单并勾选默认。":"全局默认菜单已禁用；没有匹配到其他已启用菜单时不显示悬浮窗。";return;}
        static string Normalize(string name)=>name.Trim().EndsWith(".exe",StringComparison.OrdinalIgnoreCase)?name.Trim()[..^4]:name.Trim();
        var names=p.Applications.Select(a=>Normalize(a.ProcessName)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var conflicts=draft.Profiles.Where(other=>other.Id!=p.Id&&!other.IsDefault&&other.Enabled&&other.Applications.Any(a=>names.Contains(Normalize(a.ProcessName)))).ToArray();
        matchWarning.Text=conflicts.Length>0?"重复匹配："+string.Join("、",conflicts.Select(c=>c.Name))+"。启用菜单中优先级高的生效；同级按菜单标识固定选择。"
            :p.Applications.Count==0?"尚未选择应用，此菜单暂不自动匹配。":"优先级只在多个启用菜单匹配同一应用时生效，数值越大越优先。";
    }
    private Button RotationButton(int direction)
    {
        var button=MakeButton("",(_,_)=>RotateSelectedRing(direction));
        button.Width=button.Height=48;button.HorizontalAlignment=HorizontalAlignment.Center;
        var figure=new PathFigure{StartPoint=new(21,12),IsClosed=false};
        figure.Segments.Add(new ArcSegment{Point=new(12,3),Size=new(9,9),IsLargeArc=true,SweepDirection=SweepDirection.Clockwise});
        var geometry=new PathGeometry();geometry.Figures.Add(figure);
        var arrow=new PathFigure{StartPoint=new(7,3),IsClosed=false};
        arrow.Segments.Add(new LineSegment{Point=new(12,3)});arrow.Segments.Add(new LineSegment{Point=new(12,8)});geometry.Figures.Add(arrow);
        var icon=new Microsoft.UI.Xaml.Shapes.Path{Data=geometry,Stroke=Brush(224,233,249),StrokeThickness=2,Width=24,Height=24};
        if(direction<0){icon.RenderTransformOrigin=new(0.5,0.5);icon.RenderTransform=new ScaleTransform{ScaleX=-1};}
        button.Content=icon;
        var label=(direction<0?"逆时针":"顺时针")+"旋转半个按钮";
        ToolTipService.SetToolTip(button,label+"（180° / 当前圈按钮数）");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button,label);return button;
    }
    private void RotateSelectedRing(int direction)
    {
        if(rings.SelectedItem is not ComboBoxItem item||(int)item.Tag<0)return;
        CancelDrag();draft.RotateRing(profileId,(int)item.Tag,direction);MarkDirty();RefreshPreview();
    }
    private StackPanel RotationArea(int direction)
    {
        var area=new StackPanel{Width=96,Spacing=4,Margin=new Thickness(4),VerticalAlignment=VerticalAlignment.Top,
            HorizontalAlignment=direction<0?HorizontalAlignment.Left:HorizontalAlignment.Right};
        area.Children.Add(RotationButton(direction));
        area.Children.Add(new TextBlock{Text=direction<0?"逆时针旋转":"顺时针旋转",FontSize=11,TextAlignment=TextAlignment.Center});
        return area;
    }
    private void QueueAddRing(bool inner)
    {
        addingRing=true;rings.IsDropDownOpen=false;var target=profileId;
        DispatcherQueue.TryEnqueue(()=>
        {
            try
            {
                if(profileId!=target)return;
                CancelDrag();var ring=draft.AddRing(profileId,inner);MarkDirty();
                // Retain the add item while its popup is closing; clearing the selected
                // container here can make WinUI's deferred selection animation fail.
                if(inner)
                    foreach(var existing in rings.Items.Cast<ComboBoxItem>().Where(i=>(int)i.Tag>=0))
                    {
                        var shifted=(int)existing.Tag+1;existing.Tag=shifted;existing.Content=$"第 {shifted+1} 圈";
                    }
                var item=new ComboBoxItem{Content=$"第 {ring+1} 圈",Tag=ring};
                rings.Items.Insert(ring+1,item);rings.SelectedItem=item;RefreshPreview();
            }
            catch(Exception ex){RefreshRings(0);Notify(ex.Message,InfoBarSeverity.Error);}
            finally{addingRing=false;}
        });
    }
    internal void RefreshDisplaySize()
    {
        var menu=draft.Find(profileId);var requested=new MultiRingLayout(menu).DisplayDiameter;
        var diameter=OverlayWindow.RingDiameterForWindow(OwnerWindowHandle,requested);
        if(Math.Abs(previewBox.Width-diameter)>0.01||double.IsNaN(previewBox.Width))
        {
            if(drag is not null)CancelDrag(restorePreview:false);
            previewBox.Width=previewBox.Height=diameter;
            previewScroll.Height=Math.Min(600,diameter);
        }
        sizeDescription.Text=$"{menu.SizeScale*100:0}% · 直径 {diameter:0}"+(diameter<requested-1?"（已适配屏幕）":"")+" · 与实际悬浮窗等大";
    }
    private async Task ImportSingleMenuAsync()
    {
        var document=ImportMenuRequested is null?null:await ImportMenuRequested();if(document is null)return;
        var same=draft.Profiles.FirstOrDefault(p=>p.Name.Equals(document.Menu.Name,StringComparison.OrdinalIgnoreCase));
        string? overwrite=null;
        if(same is not null)
        {
            dialogOpen=true;
            try
            {
                var result=await ShowDialogAsync(new ContentDialog{Title="已存在同名菜单",Content=$"“{same.Name}”已存在。覆盖会替换该菜单；另存会自动在名称后添加 +1、+2…",
                    PrimaryButtonText="覆盖",SecondaryButtonText="另存（自动改名）",CloseButtonText="取消",XamlRoot=XamlRoot});
                if(result==ContentDialogResult.None)return;if(result==ContentDialogResult.Primary)overwrite=same.Id;
            }
            finally{dialogOpen=false;}
        }
        CancelDrag();profileId=draft.ImportMenu(document,overwrite);selectedButtonId=null;
        PopulateActionChoices();MarkDirty();RefreshProfiles();
    }
}