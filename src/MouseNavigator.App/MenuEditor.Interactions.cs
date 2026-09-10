using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using Windows.Foundation;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    private MenuDragSession? drag;
    private MenuProfile? dropProposal;
    private Microsoft.UI.Xaml.Input.Pointer? capturedPointer;
    private Point dragStart;
    private bool moved;
    private string? hoverKey;
    private Border? dragGhost;
    private Microsoft.UI.Xaml.Media.GeneralTransform? dragWindowToPreview;
    private Point? lastDropWindowPoint;
    private bool lastOverTrash,overPresetDrop;
    private void BeginDrag(string? source,ButtonPreset? preset,PointerRoutedEventArgs e)
    {
        if(dialogOpen)return;CancelDrag();
        drag=new(draft.Find(profileId),source,preset);dragStart=e.GetCurrentPoint(root).Position;
        moved=false;capturedPointer=e.Pointer;
        if(!root.CapturePointer(e.Pointer)){drag=null;capturedPointer=null;return;}
        IsTabStop=true;Focus(FocusState.Programmatic);
        if(source is not null){selectedButtonId=source;RefreshButton();}
        e.Handled=true;
    }
    private void UpdateDragGhost(Point p)
    {
        if(drag is null)return;
        if(dragGhost is null)
        {
            var visual=preview.Buttons.FirstOrDefault(b=>b.Id==drag.SourceId);
            var content=new StackPanel{Spacing=5,HorizontalAlignment=HorizontalAlignment.Center};
            content.Children.Add(ButtonIcons.Create(drag.Preset?.Glyph??visual?.Glyph,drag.Preset?.Image??visual?.Image,28));
            content.Children.Add(new TextBlock{Text=drag.Preset?.Name??visual?.Label??"按钮",FontSize=12,MaxWidth=96,MaxLines=2,
                TextWrapping=TextWrapping.Wrap,TextTrimming=TextTrimming.CharacterEllipsis,TextAlignment=TextAlignment.Center});
            dragGhost=new Border{Child=content,Width=112,Padding=new Thickness(8),CornerRadius=new CornerRadius(12),
                Background=Brush(48,77,124),BorderBrush=Brush(126,178,255),BorderThickness=new Thickness(1),Opacity=0.95,IsHitTestVisible=false};
            dragLayer.Children.Add(dragGhost);
        }
        Canvas.SetLeft(dragGhost,p.X-56);Canvas.SetTop(dragGhost,p.Y-34);
        preview.SetDraggedButton(drag.SourceId??drag.NewButtonId);
    }
    private void DragMoved(object sender,PointerRoutedEventArgs e)
    {
        if(drag is null)return;
        var p=e.GetCurrentPoint(root).Position;
        if(!moved&&Math.Abs(p.X-dragStart.X)+Math.Abs(p.Y-dragStart.Y)<7)return;
        moved=true;UpdateDragGhost(e.GetCurrentPoint(dragLayer).Position);
        UpdateDropFromWindow(e.GetCurrentPoint(null).Position);
        e.Handled=true;
    }
    private bool UpdateDropFromWindow(Point windowPoint,bool force=false)
    {
        // Snapshot the coordinate mapping once: animated visuals and relayout must
        // never move the insertion grid underneath a stationary physical pointer.
        dragWindowToPreview??=XamlRoot.Content.TransformToVisual(preview);
        var overTrash=IsTrashPoint(XamlRoot.Content.TransformToVisual(trash).TransformPoint(windowPoint));
        var savePoint=XamlRoot.Content.TransformToVisual(savePreset).TransformPoint(windowPoint);
        var wasOverPreset=overPresetDrop;
        overPresetDrop=savePoint.X>=0&&savePoint.Y>=0&&savePoint.X<=savePreset.ActualWidth&&savePoint.Y<=savePreset.ActualHeight;
        savePreset.Background=overPresetDrop?Brush(40,110,80):Brush(38,44,56);
        if(!force&&wasOverPreset==overPresetDrop&&lastDropWindowPoint is Point last&&overTrash==lastOverTrash
            &&Math.Abs(windowPoint.X-last.X)<3&&Math.Abs(windowPoint.Y-last.Y)<3)return overTrash;
        lastDropWindowPoint=windowPoint;lastOverTrash=overTrash;
        if(overPresetDrop)
        {
            SetTrashHighlight(false);
            if(hoverKey!="preset"){hoverKey="preset";dropProposal=null;RefreshPreview();dragHint.Text=drag?.SourceId is not null?"松开保存为独立预设按钮":"此按钮已在预设栏中，松开取消";}
            return false;
        }
        var viewportPoint=XamlRoot.Content.TransformToVisual(previewScroll).TransformPoint(windowPoint);
        if(!overTrash&&(viewportPoint.X<0||viewportPoint.Y<0||viewportPoint.X>previewScroll.ActualWidth||viewportPoint.Y>previewScroll.ActualHeight))
        {
            dropProposal=null;hoverKey=null;SetTrashHighlight(false);RefreshPreview();return false;
        }
        var local=dragWindowToPreview.TransformPoint(windowPoint);
        PreviewDragPosition(local.X-preview.Diameter/2,local.Y-preview.Diameter/2,overTrash);
        return overTrash;
    }
    private bool IsTrashPoint(Point p)=>p.X>=0&&p.Y>=0&&p.X<=trash.ActualWidth&&p.Y<=trash.ActualHeight;
    private void SetTrashHighlight(bool active)
    {
        trash.Background=active?Brush(143,43,55):Brush(45,37,45);
        trash.BorderBrush=active?Brush(255,137,144):Brush(93,67,71);
    }
    private void PreviewDragPosition(double x,double y,bool overTrash)
    {
        SetTrashHighlight(overTrash&&drag?.SourceId is not null);
        if(overTrash)
        {
            if(hoverKey=="trash")return;
            dropProposal=null;hoverKey="trash";RefreshPreview();
            dragHint.Text=drag?.SourceId is not null?"松开后确认删除":"预设仍保留在侧边，松开取消添加";
        }
        else PreviewDrop(x,y);
    }
    private void PreviewDrop(double x,double y)
    {
        if(drag is null)return;
        var ring=new MultiRingLayout(drag.Original).RingAt(x,y);
        var index=ring is int value?drag.InsertionIndex(value,x,y):-1;
        var key=$"{ring}:{index}";if(key==hoverKey)return;hoverKey=key;
        dropProposal=null;
        try
        {
            if(ring is int r)
            {
                dropProposal=drag.PreviewAt(r,index);RefreshPreview(dropProposal);
                preview.Highlight(drag.SourceId??drag.NewButtonId);
                dragHint.Text=$"松开放到第 {r+1} 圈的第 {index+1} 个位置";
            }
            else {RefreshPreview();dragHint.Text="移入圈内放置 · 拖到垃圾桶删除 · 此处松开取消";}
        }
        catch(ArgumentException ex){RefreshPreview();dragHint.Text=ex.Message;}
    }
    private async void DragReleased(object sender,PointerRoutedEventArgs e)
    {
        if(drag is null)return;
        var overTrash=moved&&UpdateDropFromWindow(e.GetCurrentPoint(null).Position,force:true);
        e.Handled=true;await CompleteDropAsync(overTrash);
    }
    private async Task CompleteDropAsync(bool overTrash)
    {
        if(drag is null)return;
        var current=drag;var wasMoved=moved;var proposal=dropProposal;var saveAsPreset=overPresetDrop;
        CancelDrag(restorePreview:proposal is null||overTrash);
        if(!wasMoved)return;
        try
        {
            if(saveAsPreset&&current.SourceId is not null)SaveSelectedPreset(current.SourceId);
            else if(overTrash&&current.SourceId is not null)
            {
                selectedButtonId=current.SourceId;await RemoveButtonAsync();
            }
            else if(proposal is not null&&!overTrash)
            {
                draft.CommitDrop(profileId,proposal,current.Preset,current.NewButtonId);
                selectedButtonId=current.SourceId??current.NewButtonId;
                MarkDirty();PopulateActionChoices();RefreshButton();
            }
        }
        catch(Exception ex){Notify(ex.Message,InfoBarSeverity.Error);RefreshButton();}
    }
    private void CancelDrag(bool restorePreview=true)
    {
        var had=drag is not null;drag=null;dropProposal=null;hoverKey=null;moved=false;
        dragWindowToPreview=null;lastDropWindowPoint=null;lastOverTrash=false;overPresetDrop=false;savePreset.Background=Brush(38,44,56);
        dragLayer.Children.Clear();dragGhost=null;preview.SetDraggedButton(null);SetTrashHighlight(false);
        var pointer=capturedPointer;capturedPointer=null;
        if(pointer is not null)root.ReleasePointerCapture(pointer);
        if(had&&restorePreview)RefreshPreview();
        dragHint.Text="拖动插入位置 · 拖到垃圾桶删除 · Esc 取消";
    }
    private void RefreshCenterSettings()
    {
        var prior=loading;loading=true;
        try
        {
            var p=draft.Find(profileId);centerName.Text=p.CenterText??"松开执行";
            clearCenterImage.IsEnabled=p.CenterImage is not null||p.CenterGlyph is not null;
            var content=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};
            content.Children.Add(ButtonIcons.Create(p.CenterGlyph??"\uEB9F",p.CenterImage,20));
            content.Children.Add(new TextBlock{Text="选择图标 / 图片"});centerImagePicker.Content=content;
        }
        finally{loading=prior;}
    }
    private async Task ChooseCenterImageAsync()
    {
        if(dialogOpen)return;dialogOpen=true;
        try
        {
            var image=PickImageRequested is null?null:await PickImageRequested();if(image is null)return;
            draft.SetCenterAppearance(profileId,draft.Find(profileId).CenterText,image);MarkDirty();RefreshCenterSettings();RefreshPreview();
        }
        catch(Exception ex){Notify(ex.Message,InfoBarSeverity.Error);}
        finally{dialogOpen=false;}
    }
    private async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        var operation=dialog.ShowAsync();
#if DEBUG
        if(DialogOpenedForSmoke is not null)await DialogOpenedForSmoke(dialog);
#endif
        return await operation;
    }
    private async Task<bool> ConfirmAsync(string title,string text)
    {
        if(dialogOpen)return false;dialogOpen=true;
        try{return await ShowDialogAsync(new ContentDialog{Title=title,Content=new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap},PrimaryButtonText="删除",CloseButtonText="取消",DefaultButton=ContentDialogButton.Close,XamlRoot=XamlRoot})==ContentDialogResult.Primary;}
        finally{dialogOpen=false;}
    }
    private async Task RemoveButtonAsync()
    {
        var id=selectedButtonId;if(id is null)return;
        if(!await ConfirmAsync("删除按钮","删除这个按钮？其他按钮会重新排列，预设按钮不会改变。"))return;
        draft.RemoveButton(profileId,id);selectedButtonId=null;MarkDirty();RefreshButton();
    }
    private async Task RemoveRingAsync()
    {
        if(rings.SelectedItem is not ComboBoxItem item||(int)item.Tag<0)return;CancelDrag();var ring=(int)item.Tag;
        var count=draft.Find(profileId).Entries.Count(e=>e.Ring==ring);
        if(count>0&&!await ConfirmAsync("删除圈",$"删除第 {ring+1} 圈及其中的 {count} 个按钮？"))return;
        draft.RemoveRing(profileId,ring);selectedButtonId=null;MarkDirty();RefreshRings(Math.Max(0,ring-1));RefreshButton();
    }
    private async Task RenameAsync(bool create)
    {
        if(dialogOpen)return;CancelDrag();dialogOpen=true;
        var name=new TextBox{Text=create?"":draft.Find(profileId).Name,PlaceholderText="输入菜单名称",MaxLength=80};
        var dialog=new ContentDialog {Title=create?"新建悬浮菜单":"重命名菜单",Content=name,PrimaryButtonText=create?"创建":"确定",CloseButtonText="取消",DefaultButton=ContentDialogButton.Primary,XamlRoot=XamlRoot};
        dialog.PrimaryButtonClick+=(_,args)=>{if(string.IsNullOrWhiteSpace(name.Text)){args.Cancel=true;name.Header="名称不能为空";}};
        try
        {
            if(await ShowDialogAsync(dialog)!=ContentDialogResult.Primary)return;
            if(create)profileId=draft.Add();var p=draft.Find(profileId);
            draft.Update(profileId,name.Text.Trim(),p.Applications,p.Priority);MarkDirty();RefreshProfiles();
        }
        finally{dialogOpen=false;}
    }
    private void SaveSelectedPreset(string? id=null)
    {
        id??=selectedButtonId;if(id is null||id==RadialMenuView.CenterButtonId)return;
        try
        {
            var visual=preview.Buttons.Single(b=>b.Id==id);
            draft.SavePreset(profileId,id,visual.Label,visual.Glyph);MarkDirty();RefreshPresets();Notify("已保存为独立预设。");
        }
        catch(Exception ex){Notify(ex.Message,InfoBarSeverity.Error);}
    }
    private void RefreshPresets()
    {
        presetCards.Children.Clear();
        var builtins=actions.Select(a=>new ButtonPreset("builtin-"+a.Id,a.Name,a.Id,a.Glyph)).Append(new("builtin-shortcut","自定义快捷键","shortcuts.template","\uE765",Keys:new ushort[]{17,75}));
        foreach(var preset in new[]{new ButtonPreset("builtin-blank","空白按钮","","\uE710")}.Concat(builtins).Concat(draft.Presets))
        {
            var stack=new StackPanel {Spacing=5};
            var heading=new StackPanel {Orientation=Orientation.Horizontal,Spacing=7};heading.Children.Add(ButtonIcons.Create(preset.Glyph,preset.Image,22));
            heading.Children.Add(new TextBlock {Text=preset.Name,FontSize=12,TextWrapping=TextWrapping.Wrap,MaxWidth=100});stack.Children.Add(heading);
            stack.Children.Add(new TextBlock {Text=preset.Keys is not null?ShortcutKeys.Format(preset.Keys):actions.FirstOrDefault(a=>a.Id==preset.ActionId)?.Name??preset.ActionId,FontSize=10,Opacity=0.6,TextTrimming=TextTrimming.CharacterEllipsis});
            var card=new Border {Child=stack,Padding=new Thickness(10),CornerRadius=new CornerRadius(10),Background=Brush(38,44,56)};
            card.PointerPressed+=(_,e)=>{if(e.GetCurrentPoint(card).Properties.IsLeftButtonPressed)BeginDrag(null,preset,e);};
            if(draft.Presets.Any(p=>p.Id==preset.Id))
            {
                var menu=new MenuFlyout();var remove=new MenuFlyoutItem{Text="删除此预设"};
                remove.Click+=(_,_)=>{draft.RemovePreset(preset.Id);MarkDirty();RefreshPresets();};menu.Items.Add(remove);card.ContextFlyout=menu;
            }
            presetCards.Children.Add(card);
        }
    }
    private async Task ShowIconsAsync()
    {
        var forCenter=selectedButtonId==RadialMenuView.CenterButtonId;
        var entry=Entry;if((entry is null&&!forCenter)||dialogOpen)return;dialogOpen=true;
        void ApplyIcon(string? glyph,string? image=null)
        {
            if(forCenter)draft.SetCenterAppearance(profileId,draft.Find(profileId).CenterText,image,glyph);
            else draft.SetAppearance(profileId,entry!.Id,entry.Label,glyph,image);
        }
        string? selected=null;
        var grid=new Grid {RowSpacing=8,ColumnSpacing=8};for(var i=0;i<6;i++)grid.ColumnDefinitions.Add(new(){Width=new GridLength(48)});
        var codes=new[]{0xE734,0xE737,0xE74E,0xE8B7,0xE721,0xE713,0xE768,0xE765,0xE7C4,0xE8A7,0xE77F,0xE80F,0xE8F1,0xE74D,0xE710,0xE711,0xE8D4,0xE8A5,0xE8B0,0xE8EF,0xE8A1,0xE8D1,0xE8B5,0xE8B8,0xE7F4,0xE8D2,0xE753,0xE787,0xE715,0xE72D};
        var dialog=new ContentDialog{Title=forCenter?"选择中心图标":"选择按钮图标",Content=grid,PrimaryButtonText="选择图片…",SecondaryButtonText=forCenter?"不显示图标":"跟随动作",CloseButtonText="取消",XamlRoot=XamlRoot};
        for(var i=0;i<codes.Length;i++)
        {
            if(i%6==0)grid.RowDefinitions.Add(new());var glyph=char.ConvertFromUtf32(codes[i]);
            var button=new Button {Content=ButtonIcons.Create(glyph,null),Width=48,Height=48};
            button.Click+=(_,_)=>{selected=glyph;dialog.Hide();};Grid.SetColumn(button,i%6);Grid.SetRow(button,i/6);grid.Children.Add(button);
        }
        try
        {
            var result=await ShowDialogAsync(dialog);
            if(selected is not null)ApplyIcon(selected);
            else if(result==ContentDialogResult.Secondary)ApplyIcon(null);
            else if(result==ContentDialogResult.Primary)
            {
                var image=PickImageRequested is null?null:await PickImageRequested();if(image is null)return;
                ApplyIcon(null,image);
            }
            else return;
            MarkDirty();RefreshButton();
        }
        catch(Exception ex){Notify(ex.Message,InfoBarSeverity.Error);}
        finally{dialogOpen=false;}
    }
}