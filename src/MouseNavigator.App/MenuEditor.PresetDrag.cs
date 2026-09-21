using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MouseNavigator.Contracts;
using Windows.Foundation;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    private readonly ScrollViewer presetScroll=new(){HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
    private sealed record PresetDrop(bool IntoFolder,string? FolderId,string? BeforeId);
    private PresetDrop? presetDrop;
    private readonly List<(Border Card,ButtonPreset Preset)> visiblePresetCards=[];
    private Border? presetInsertion;
    private Button? highlightedFolder;
    private readonly DispatcherTimer presetDragScroll=new(){Interval=TimeSpan.FromMilliseconds(40)};
    private Point? presetDragPoint;
    private bool presetScrollInitialized;

    private bool Inside(FrameworkElement element,Point windowPoint,out Point local)
    {
        local=XamlRoot.Content.TransformToVisual(element).TransformPoint(windowPoint);
        return local.X>=0&&local.Y>=0&&local.X<=element.ActualWidth&&local.Y<=element.ActualHeight;
    }
    private void ClearPresetDrop()
    {
        presetDrop=null;
        if(presetInsertion is not null){dragLayer.Children.Remove(presetInsertion);presetInsertion=null;}
        if(highlightedFolder is not null){highlightedFolder.Background=presetThemeBrush;highlightedFolder=null;}
    }
    private void ShowPresetFolderDrop(Button button,string? folderId)
    {
        presetDrop=new(true,folderId,null);highlightedFolder=button;
        button.Background=Brush(40,110,80);
        dragHint.Text=drag?.Preset is {} p&&draft.Presets.Any(x=>x.Id==p.Id)
            ?"松开移入 "+FolderPath(folderId):"松开将独立副本存入 "+FolderPath(folderId);
    }
    private bool UpdatePresetDrop(Point windowPoint)
    {
        if(highlightedFolder is not null&&drag?.Preset is not null&&Inside(presetScroll,windowPoint,out _)&&Inside(highlightedFolder,windowPoint,out _))return true;
        ClearPresetDrop();
        if(drag?.Preset is null)return false;
        if(drag.Preset.Id=="builtin-blank")
        {
            if(Inside(presetScroll,windowPoint,out _)||Inside(presetBack,windowPoint,out _)){dragHint.Text="空白按钮固定在根目录首位，只能拖入轮盘";return true;}
            return false;
        }
        if(presetBack.IsEnabled&&Inside(presetScroll,windowPoint,out _)&&Inside(presetBack,windowPoint,out _))
        {
            ShowPresetFolderDrop(presetBack,currentPresetFolder?.StartsWith("$")==true?null:
                draft.PresetFolders.FirstOrDefault(f=>f.Id==currentPresetFolder)?.ParentId);
            return true;
        }
        if(!Inside(presetScroll,windowPoint,out _))return false;
        foreach(var folder in presetCards.Children.OfType<Button>())
            if(Inside(folder,windowPoint,out _))
            {
                if(folder.Tag is string id&&!id.StartsWith("$"))ShowPresetFolderDrop(folder,id);
                else dragHint.Text="内置分类不能存放个人预设，请拖入个人文件夹";
                return true;
            }
        var candidates=visiblePresetCards.Where(x=>x.Preset.Id!=drag.Preset.Id&&x.Preset.Id!="builtin-blank").ToArray();
        var before=candidates.FirstOrDefault(x=>{
            var p=XamlRoot.Content.TransformToVisual(x.Card).TransformPoint(windowPoint);
            return p.Y<x.Card.ActualHeight/2;
        });
        if(!visiblePresetCards.Any(x=>x.Preset.Id==drag.Preset.Id))return true;
        presetDrop=new(false,null,before.Preset?.Id);
        var anchor=before.Card??candidates.LastOrDefault().Card;
        if(anchor is not null)
        {
            var start=anchor.TransformToVisual(dragLayer).TransformPoint(new Point(0,before.Card is null?anchor.ActualHeight:0));
            var top=presetScroll.TransformToVisual(dragLayer).TransformPoint(new Point(0,0)).Y;
            start.Y=Math.Clamp(start.Y,top,Math.Max(top,top+presetScroll.ActualHeight-3));
            presetInsertion=new Border{Height=3,Width=anchor.ActualWidth,Background=new Microsoft.UI.Xaml.Media.SolidColorBrush(PreviewPalette.Accent),IsHitTestVisible=false};
            Canvas.SetLeft(presetInsertion,start.X);Canvas.SetTop(presetInsertion,start.Y);
            dragLayer.Children.Add(presetInsertion);
        }
        dragHint.Text="松开插入此位置";
        return true;
    }
    private void TrackPresetScroll(Point windowPoint)
    {
        presetDragPoint=windowPoint;
        if(!presetScrollInitialized)
        {
            presetScrollInitialized=true;
            presetDragScroll.Tick+=(_,_)=>{
                if(drag?.Preset is null||!moved||presetDragPoint is not Point point||!Inside(presetScroll,point,out var local))return;
                var delta=local.Y<30?-12:local.Y>presetScroll.ActualHeight-30?12:0;
                if(delta==0)return;
                presetScroll.ChangeView(null,Math.Clamp(presetScroll.VerticalOffset+delta,0,presetScroll.ScrollableHeight),null,true);
                UpdatePresetDrop(point);
            };
        }
        presetDragScroll.Start();
    }
    private void CommitPresetDrop(ButtonPreset preset,PresetDrop target)
    {
        if(target.IntoFolder)draft.PlacePreset(preset,target.FolderId);
        else draft.ReorderPresets(visiblePresetCards.Where(x=>x.Preset.Id!="builtin-blank").Select(x=>x.Preset.Id).ToArray(),preset.Id,target.BeforeId);
        MarkDirty();RefreshPresets();
    }
}