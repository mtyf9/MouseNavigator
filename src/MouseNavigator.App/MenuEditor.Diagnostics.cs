#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    internal Func<ContentDialog,Task>? DialogOpenedForSmoke {get;set;}
    internal async Task CheckCenterOptionsAsync(List<string> results,string directory)
    {
        void Check(bool value,string text)=>results.Add((value?"PASS: ":"FAIL: ")+text);
        await Task.Delay(250);preview.SelectAt(0,0);centerName.Text="";await Task.Delay(80);
        var restored=ConfigurationCodec.Deserialize(ConfigurationCodec.Serialize(draft.Snapshot())).Profiles.Single(p=>p.Id==profileId);
        Check(restored.CenterText==""&&preview.CenterTextForSmoke=="","Empty center text stays empty in preview and saved configuration");
        DialogOpenedForSmoke=async dialog=>
        {
            await Task.Delay(120);
            var iconButton=((Grid)dialog.Content).Children.OfType<Button>().First();
            var peer=new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(iconButton);
            ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)peer.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();
        };
        try {await ShowIconsAsync();} finally {DialogOpenedForSmoke=null;}
        restored=ConfigurationCodec.Deserialize(ConfigurationCodec.Serialize(draft.Snapshot())).Profiles.Single(p=>p.Id==profileId);
        Check(restored.CenterGlyph=="\uE734"&&restored.CenterImage is null&&restored.CenterText==""&&preview.HasCenterGlyphForSmoke,
            "Center uses the selected gallery icon and preserves blank text after reload");
        await Task.Delay(200);
        var area=(Grid)trash.Parent;var position=trash.TransformToVisual(area).TransformPoint(new(0,0));
        Check(position.X>area.ActualWidth/2&&position.Y>area.ActualHeight/2&&IsTrashPoint(new(1,1)),
            "Trash is a working drop target at the lower right of the preview");
        await SmokeScenario.RenderAsync((FrameworkElement)XamlRoot.Content,Path.Combine(directory,"center-options.png"));
    }
    internal async Task CheckStationaryDragAndCenterAsync(List<string> results,string directory)
    {
        void Check(bool value,string text)=>results.Add((value?"PASS: ":"FAIL: ")+text);
        await Task.Delay(250);
        var original=draft.Snapshot();var picker=PickImageRequested;
        try
        {
            drag=new(draft.Find(profileId),"left");moved=true;
            var fixedPoint=preview.TransformToVisual(XamlRoot.Content).TransformPoint(new(preview.Diameter/2,preview.Diameter/2-100));
            UpdateDropFromWindow(fixedPoint);var proposal=dropProposal!;
            // Repeat the same window coordinates while frames advance and preview
            // refreshes arrive; neither may change the drop target or restart motion.
            for(var i=0;i<24;i++){UpdateDropFromWindow(fixedPoint);RefreshPreview(proposal);await Task.Delay(25);}
            var angle=preview.AngleForSmoke("top");await Task.Delay(100);
            Check(ReferenceEquals(proposal,dropProposal)&&!preview.IsAnimatingForSmoke&&Math.Abs(angle-preview.AngleForSmoke("top"))<0.001,
                "Stationary window coordinates keep one insertion target and settled animation");
            await CompleteDropAsync(false);
            Check(draft.Find(profileId).Entries.Select(e=>e.Id).SequenceEqual(new[]{"left","top","right","bottom"}),"Release keeps the stable insertion order");
            var entries=draft.Find(profileId).Entries.ToArray();
            preview.SelectAt(0,0);
            Check(selectedButtonId==RadialMenuView.CenterButtonId&&centerSettings.Visibility==Visibility.Visible
                &&buttonName.Visibility==Visibility.Collapsed&&properties.Children.Contains(centerSettings),"Clicking the center opens its controls in Configure button");
            centerName.Text="我的中心";
            const string png="iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aMfoAAAAASUVORK5CYII=";
            PickImageRequested=()=>Task.FromResult<string?>(png);await ChooseCenterImageAsync();
            var restored=ConfigurationCodec.Deserialize(ConfigurationCodec.Serialize(draft.Snapshot())).Profiles.Single(p=>p.Id==profileId);
            Check(preview.CenterTextForSmoke=="我的中心"&&preview.HasCenterImageForSmoke&&restored.CenterText=="我的中心"&&restored.CenterImage==png
                &&entries.SequenceEqual(restored.Entries),"Center appearance edits persist without modifying surrounding buttons");
            await Task.Delay(150);await SmokeScenario.RenderAsync((FrameworkElement)XamlRoot.Content,Path.Combine(directory,"center-editor.png"));
            preview.SelectAt(100,0);
            Check(centerSettings.Visibility==Visibility.Collapsed&&buttonName.Visibility==Visibility.Visible&&selectedButtonId!=RadialMenuView.CenterButtonId&&Entry is not null,
                "Clicking an outer button restores its configuration controls");
        }
        finally {PickImageRequested=picker;CancelDrag();Load(original,false);}
    }
    internal async Task CheckDialogsForSmokeAsync(List<string> results,string directory)
    {
        var original=draft.Snapshot();var imagePicker=PickImageRequested;
        void Check(bool value,string text)=>results.Add((value?"PASS: ":"FAIL: ")+text);
        async Task Click(ContentDialog dialog,string name)
        {
            await Task.Delay(160);
            var button=Descendants(dialog).OfType<Button>().First(b=>b.Name==name);
            var peer=new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(button);
            ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)peer.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();
        }
        try
        {
            selectedButtonId="right";RefreshButton();
            DialogOpenedForSmoke=async d=>{await Task.Delay(120);await SmokeScenario.RenderAsync(d,Path.Combine(directory,"delete-confirm.png"));await Click(d,"CloseButton");};
            var before=ConfigurationCodec.Serialize(draft.Snapshot());await RemoveButtonAsync();
            Check(before==ConfigurationCodec.Serialize(draft.Snapshot()),"Cancel in the actual delete dialog preserves the button");
            DialogOpenedForSmoke=d=>Click(d,"PrimaryButton");await RemoveButtonAsync();
            Check(draft.Find(profileId).Entries.All(e=>e.Id!="right"),"Confirm in the actual delete dialog removes the selected button");
            Load(original,true);
            DialogOpenedForSmoke=async d=>{((TextBox)d.Content).Text="新建测试菜单";await Click(d,"PrimaryButton");};
            await RenameAsync(true);
            Check(draft.Find(profileId).Name=="新建测试菜单","New menu name dialog creates a named menu");
            DialogOpenedForSmoke=async d=>{((TextBox)d.Content).Text="重命名测试";await Click(d,"PrimaryButton");};
            await RenameAsync(false);Check(draft.Find(profileId).Name=="重命名测试","Rename dialog updates the selected menu");
            Load(original,true);selectedButtonId="right";RefreshButton();
            DialogOpenedForSmoke=async d=>{await Task.Delay(120);await SmokeScenario.RenderAsync(d,Path.Combine(directory,"icon-gallery.png"));await Click(d,"PrimaryButton");};
            PickImageRequested=async()=>await ButtonIcons.ImportAsync(await global::Windows.Storage.StorageFile.GetFileFromPathAsync(Path.Combine(directory,"home.png")));
            await ShowIconsAsync();
            Check(Entry?.Image is not null,"Image icon is decoded resized embedded and displayed through the icon dialog");
            if(Entry?.Image is{}image)
            {
                ConfigurationCodec.ValidateImage(image);
                var json=ConfigurationCodec.Serialize(draft.Snapshot());Check(ConfigurationCodec.Deserialize(json).Profiles.Single(p=>p.Id==profileId).Entries.Single(e=>e.Id=="right").Image==image,"Image icon survives export and reload without a local path");
            }
        }
        finally{DialogOpenedForSmoke=null;PickImageRequested=imagePicker;Load(original,true);}
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for(var i=0;i<Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);i++)
        {
            var child=Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root,i);yield return child;
            foreach(var next in Descendants(child))yield return next;
        }
    }
    internal async Task CheckDragReorderForSmokeAsync(List<string> results,string directory)
    {
        var original=draft.Snapshot();var picker=PickImageRequested;
        void Check(bool value,string text)=>results.Add((value?"PASS: ":"FAIL: ")+text);
        async Task ClickDialog(ContentDialog d,string name)
        {
            await Task.Delay(100);
            var button=Descendants(d).OfType<Button>().Single(b=>b.Name==name);
            var peer=new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(button);
            ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)peer.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();
        }
        try
        {
            Load(original,true);await Task.Delay(300);
            var trashScreen=trash.TransformToVisual(XamlRoot.Content).TransformPoint(new(0,0));
            Check(trashScreen.Y>=0&&trashScreen.Y+trash.ActualHeight<=XamlRoot.Size.Height,"Trash remains visible at the lower right of the wheel");
            drag=new(draft.Find(profileId),"left");moved=true;
            var p=preview.TransformToVisual(dragLayer).TransformPoint(new(preview.Diameter/2,preview.Diameter/2-100));
            UpdateDragGhost(p);var before=preview.AngleForSmoke("top");PreviewDrop(0,-100);
            Check(string.Join(",",dropProposal!.Entries.Select(e=>e.Id))=="left,top,right,bottom","Existing drag inserts at the destination and shifts intervening neighbours");
            Check(preview.OpacityForSmoke("left")==0&&dragLayer.Children.Count==1,"Dragged icon has one floating copy and leaves a visible slot");
            var animationsEnabled=new global::Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
            Check(preview.IsAnimatingForSmoke==animationsEnabled,"Neighbour motion respects the Windows animation setting");
            await Task.Delay(65);
            Check(!animationsEnabled||Math.Abs(preview.AngleForSmoke("top")-before)>0.01,"Neighbour position advances during a drag animation");
            UpdateDragGhost(new(p.X+25,p.Y+15));
            Check(Math.Abs(Canvas.GetLeft(dragGhost!)-(p.X+25-56))<0.01&&Math.Abs(Canvas.GetTop(dragGhost!)-(p.Y+15-34))<0.01,"Floating drag copy follows both pointer coordinates");
            await SmokeScenario.RenderAsync(this,Path.Combine(directory,"drag-insert.png"));
            await Task.Delay(250);
            Check(!preview.IsAnimatingForSmoke&&Math.Abs(preview.AngleForSmoke("top"))<0.01,"Neighbours settle exactly at the insertion layout");
            await CompleteDropAsync(false);
            Check(string.Join(",",draft.Find(profileId).Entries.Select(e=>e.Id))=="left,top,right,bottom"&&dragLayer.Children.Count==0,"Release commits the insertion and removes the floating copy");
            var snapshot=ConfigurationCodec.Serialize(draft.Snapshot());var dialogs=0;
            DialogOpenedForSmoke=async d=>{dialogs++;await ClickDialog(d,"CloseButton");};
            drag=new(draft.Find(profileId),"left");moved=true;PreviewDragPosition(999,999,false);await CompleteDropAsync(false);
            Check(dialogs==0&&snapshot==ConfigurationCodec.Serialize(draft.Snapshot()),"Release outside the menu cancels without deleting or opening a dialog");
            Check(IsTrashPoint(new(1,1))&&!IsTrashPoint(new(-1,1))&&!IsTrashPoint(new(trash.ActualWidth+1,1)),"Trash hit target is limited to the visible trash rectangle");
            drag=new(draft.Find(profileId),"left");moved=true;PreviewDragPosition(999,999,true);await CompleteDropAsync(true);
            Check(dialogs==1&&snapshot==ConfigurationCodec.Serialize(draft.Snapshot()),"Dropping onto trash asks for confirmation and cancel preserves the button");
            DialogOpenedForSmoke=d=>ClickDialog(d,"PrimaryButton");
            drag=new(draft.Find(profileId),"left");moved=true;PreviewDragPosition(999,999,true);await CompleteDropAsync(true);
            Check(draft.Find(profileId).Entries.All(e=>e.Id!="left"),"Confirmed trash drop removes only the dragged button");
            Load(original,true);await Task.Delay(280);
            var preset=new ButtonPreset("fixture","新增任务","windows.tasks","\uE7C4");
            drag=new(draft.Find(profileId),null,preset);moved=true;var added=drag.NewButtonId;
            UpdateDragGhost(p);PreviewDrop(95,-31);
            Check(dropProposal?.Entries[1].Id==added&&preview.IsAnimatingForSmoke==animationsEnabled,"Preset insertion opens a new slot and animates the neighbours");
            await Task.Delay(250);await SmokeScenario.RenderAsync(this,Path.Combine(directory,"drag-preset.png"));
            await CompleteDropAsync(false);
            Check(draft.Find(profileId).Entries.Count==5&&draft.Find(profileId).Entries[1].Id==added,"Preset release commits the selected insertion slot");
            drag=new(draft.Find(profileId),"top");moved=true;UpdateDragGhost(p);PreviewDrop(0,100);CancelDrag();
            Check(dragLayer.Children.Count==0&&preview.OpacityForSmoke("top")==1,"Canceled drag restores the source icon and clears the overlay");
        }
        finally {DialogOpenedForSmoke=null;PickImageRequested=picker;CancelDrag();Load(original,true);}
        // Leave customized center content for the subsequent runtime/save round trip.
        try
        {
            centerName.Text="我的工具";
            PickImageRequested=async()=>await ButtonIcons.ImportAsync(await global::Windows.Storage.StorageFile.GetFileFromPathAsync(Path.Combine(directory,"home.png")));
            await ChooseCenterImageAsync();
            Check(preview.CenterTextForSmoke=="我的工具"&&preview.HasCenterImageForSmoke,"Center text and imported image appear together in the shared renderer");
            var image=draft.Find(profileId).CenterImage;
            draft.SetCenterAppearance(profileId,null,null);RefreshCenterSettings();RefreshPreview();
            Check(!preview.HasCenterImageForSmoke&&preview.CenterTextForSmoke!="我的工具","Reset center restores dynamic hints and removes the image");
            draft.SetCenterAppearance(profileId,"我的工具",image);RefreshCenterSettings();RefreshPreview();MarkDirty();
            preview.SelectAt(0,0);
            await Task.Delay(250);await SmokeScenario.RenderAsync(this,Path.Combine(directory,"center-settings.png"));
            selectedButtonId="right";RefreshButton();
        }
        finally {PickImageRequested=picker;}
    }
    internal RadialMenuView PreviewForSmoke=>preview;
    internal string? SelectedButtonForSmoke=>selectedButtonId;
    internal NavigatorConfiguration DraftForSmoke=>draft.Snapshot();
    internal void EditShortcutForSmoke()
    {
        selectedButtonId="right";RefreshButton();actionChoice.SelectedItem=actionChoice.Items.Cast<ComboBoxItem>().Single(i=>(string)i.Tag=="$shortcut");
        shortcutName.Text="保存文件";mainKey.SelectedItem=mainKey.Items.Cast<ComboBoxItem>().Single(i=>(ushort)i.Tag==83);
    }
    internal async Task CheckOpenSelectionsForSmokeAsync(List<string> results)
    {
        var stable=true;
        foreach(var id in new[]{"windows.maximize","$shortcut","","windows.window.preview","windows.tasks"})
        {
            actionChoice.IsDropDownOpen=true;await Task.Delay(80);
            var item=actionChoice.Items.Cast<ComboBoxItem>().Single(i=>(string)i.Tag==id);actionChoice.SelectedItem=item;actionChoice.IsDropDownOpen=false;await Task.Delay(80);
            stable&=ReferenceEquals(item,actionChoice.SelectedItem);
        }
        results.Add((stable?"PASS: ":"FAIL: ")+"Action dropdown retains selected containers");
    }
    internal async Task CheckButtonEditingForSmokeAsync(List<string> results)
    {
        void Check(bool value,string text)=>results.Add((value?"PASS: ":"FAIL: ")+text);
        var original=draft.Snapshot();
        var ring=draft.AddRing(profileId);RefreshRings(ring);RefreshPreview();
        var preset=new ButtonPreset("fixture","独立保存","shortcuts.template","\uE74E",Keys:new ushort[]{17,83});
        drag=new(draft.Find(profileId),null,preset);moved=true;
        PreviewDrop(0,-210);
        Check(dropProposal?.Entries.Count==5&&draft.Find(profileId).Entries.Count==4,"Preset hover previews outer-ring insertion without changing draft");
        var id=drag.NewButtonId;draft.CommitDrop(profileId,dropProposal!,preset,id);CancelDrag();selectedButtonId=id;RefreshButton();
        buttonName.Text="我的保存";await Task.Delay(80);
        var visual=preview.Buttons.Single(b=>b.Id==id);var savedPreset=draft.SavePreset(profileId,id,visual.Label,visual.Glyph);
        draft.SetShortcut(profileId,id,"另一个操作",new ushort[]{17,67});
        Check(savedPreset.Keys!.SequenceEqual(new ushort[]{17,83})&&savedPreset.Name=="我的保存","Saved preset is independent of later button edits");
        var before=ConfigurationCodec.Serialize(draft.Snapshot());
        drag=new(draft.Find(profileId),"right");moved=true;PreviewDrop(0,-210);
        Check(preview.Buttons.Single(b=>b.Id=="right").Ring==1,"Dragging across rings dynamically previews insertion");
        CancelDrag();Check(before==ConfigurationCodec.Serialize(draft.Snapshot()),"Cancel restores original buttons and ring placement");
        // Use the same drop proposal and commit operations as the pointer release handler.
        drag=new(draft.Find(profileId),"right");PreviewDrop(0,-210);var proposal=dropProposal!;CancelDrag();draft.CommitDrop(profileId,proposal,null,null);
        Check(draft.Find(profileId).Entries.Single(e=>e.Id=="right").Ring==1,"Committed cross-ring insertion keeps stable button identity");
        draft.RemoveRing(profileId,1);RefreshRings(0);RefreshButton();
        Check(draft.Find(profileId).RingCount==1&&draft.Find(profileId).Entries.All(e=>e.Ring==0),"Removing a ring removes its buttons and keeps inner rings");
        // Return to the original profile and leave a two-ring specimen for screenshot/save validation.
        Load(original,true);ring=draft.AddRing(profileId);
        for(var i=0;i<4;i++)
        {
            var p=new ButtonPreset("fixture"+i,"外圈 "+(i+1),"windows.tasks",i%2==0?"\uE734":"\uE8B7");
            var session=new MenuDragSession(draft.Find(profileId),null,p);
            draft.CommitDrop(profileId,session.Preview(ring,null),p,session.NewButtonId);
        }
        selectedButtonId="right";RefreshPresets();RefreshRings(0);RefreshButton();MarkDirty();
        Check(profiles.Items.Cast<ComboBoxItem>().Any(i=>(string)i.Tag=="$new"),"New floating menu entry is inside menu dropdown");
    }
}
#endif