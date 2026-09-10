#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using MouseNavigator.Windows;
using Windows.Foundation;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    internal async Task CheckInnerRingAsync(List<string> results,string directory)
    {
        void Check(bool value,string text)=>results.Add((value?"PASS: ":"FAIL: ")+text);
        var original=draft.Snapshot();
        try
        {
            await Task.Delay(250);
            draft.RotateRing(profileId,0,1);RefreshPreview();
            var before=draft.Find(profileId);
            Check((int)((ComboBoxItem)rings.Items[0]).Tag==-2,"Add inner ring is the first dropdown option");
            rings.IsDropDownOpen=true;await Task.Delay(80);rings.SelectedIndex=0;
            for(var i=0;i<30&&(addingRing||draft.Find(profileId).RingCount==before.RingCount);i++)await Task.Delay(40);
            var after=draft.Find(profileId);
            Check(after.RingCount==before.RingCount+1&&after.Entries.All(e=>e.Ring==1)&&
                after.Entries.Select(e=>e with{Ring=e.Ring-1}).SequenceEqual(before.Entries)&&
                after.RingRotations?.GetValueOrDefault(1)==before.RingRotations?.GetValueOrDefault(0)&&
                (int)((ComboBoxItem)rings.SelectedItem).Tag==0,
                "Adding an inner ring selects the empty first ring and preserves shifted buttons and rotation");
            var reloaded=ConfigurationCodec.Deserialize(ConfigurationCodec.Serialize(draft.Snapshot())).Profiles.Single(p=>p.Id==profileId);
            Check(reloaded.Entries.SequenceEqual(after.Entries)&&reloaded.RingRotations?.GetValueOrDefault(1)==after.RingRotations?.GetValueOrDefault(1),
                "The shifted ring layout survives configuration round trip");
            await RemoveRingAsync();await Task.Delay(100);
            Check(draft.Find(profileId).Entries.SequenceEqual(before.Entries)&&
                draft.Find(profileId).RingRotations?.GetValueOrDefault(0)==before.RingRotations?.GetValueOrDefault(0),
                "Deleting the new empty inner ring restores the original layout");
            rings.IsDropDownOpen=true;await Task.Delay(80);
            rings.SelectedItem=rings.Items.Cast<ComboBoxItem>().Single(i=>(int)i.Tag==-1);
            for(var i=0;i<30&&(addingRing||draft.Find(profileId).RingCount==before.RingCount);i++)await Task.Delay(40);
            Check(draft.Find(profileId).Entries.SequenceEqual(before.Entries)&&(int)((ComboBoxItem)rings.SelectedItem).Tag==before.RingCount,
                "Adding an outer ring still selects the new ring after inserting the first dropdown option");

            var area=(Grid)previewScroll.Parent;
            var tools=(FrameworkElement)rings.Parent;
            var sliderTools=(FrameworkElement)menuSize.Parent;
            double Center(FrameworkElement item)=>item.TransformToVisual(area).TransformPoint(new(item.ActualWidth/2,0)).X;
            Check(Grid.GetRow(tools)==2&&Math.Abs(Center(tools)-area.ActualWidth/2)<1&&
                Grid.GetRow(sliderTools)==0&&Math.Abs(Center(sliderTools)-area.ActualWidth/2)<1&&menuSize.ActualWidth<=280,
                "Circle tools are centered on the bottom row; the shorter slider is centered on the rotation row");
            var designer=(FrameworkElement)area.Parent;var editorGrid=(Grid)designer.Parent;
            var left=editorGrid.Children.OfType<Border>().Single(c=>Grid.GetColumn(c)==0);
            var right=editorGrid.Children.OfType<Border>().Single(c=>Grid.GetColumn(c)==2);
            var equal=true;
            foreach(var size in new[]{140,60})
            {
                menuSize.Value=size;await Task.Delay(180);
                equal&=Math.Abs(left.ActualHeight-right.ActualHeight)<1;
            }
            Check(equal,"Preset and configuration panels remain equal-height when the preview grows and shrinks");
            Load(DefaultProfiles.Configuration(),false);await Task.Delay(180);
            await SmokeScenario.RenderAsync((FrameworkElement)Content,Path.Combine(directory,"inner-ring-editor.png"));
        }
        finally{Load(original,false);}
    }
    internal async Task CheckLayoutAsync(List<string> results,string directory)
    {
        void Check(bool value,string text)
        {
            results.Add((value?"PASS: ":"FAIL: ")+text);
            File.WriteAllLines(Path.Combine(directory,"progress.txt"),results);
        }
        await Task.Delay(250);
        static bool Near(double a,double b)=>Math.Abs(a-b)<0.001;
        static bool Cardinal(MenuProfile p,int ring)
        {
            var n=p.Entries.Count(e=>e.Ring==ring);var angle=new MultiRingLayout(p).Rotation(ring);
            return n==0||Enumerable.Range(0,n).Any(i=>Math.Abs(Math.Sin(2*(-90+angle+(i+0.5)*360/n)*Math.PI/180))<0.000001);
        }
        var original=draft.Snapshot();
        try
        {
            var copy=new ConfigurationDraft(DefaultProfiles.Configuration());
            copy.RotateRing("global",0,1);
            var added=copy.AddButton("global");
            Check(Near(new MultiRingLayout(copy.Find("global")).Rotation(0),36)&&Cardinal(copy.Find("global"),0),
                "Adding a fifth button after 45-degree rotation floors to 36 degrees and a cardinal boundary");
            copy.RemoveButton("global",added);
            Check(Near(new MultiRingLayout(copy.Find("global")).Rotation(0),315)&&Cardinal(copy.Find("global"),0),
                "Deleting back to four buttons floors through zero to -45 degrees instead of resetting");
            var blank=new ButtonPreset("blank","空白按钮","");
            var session=new MenuDragSession(copy.Find("global"),null,blank);
            var proposal=session.PreviewAt(0,2);
            var radians=(-90+new MultiRingLayout(proposal).Rotation(0)+2*72)*Math.PI/180;
            Check(Cardinal(proposal,0)&&session.InsertionIndex(0,100*Math.Cos(radians),100*Math.Sin(radians))==2,
                "Insertion target uses the same snapped orientation as the animated preview");
            copy.CommitDrop("global",proposal,blank,session.NewButtonId);
            Check(Near(new MultiRingLayout(copy.Find("global")).Rotation(0),new MultiRingLayout(proposal).Rotation(0)),
                "Committing a preset preserves the preview angle");
            var before=new MultiRingLayout(copy.Find("global")).Rotation(0);
            session=new(copy.Find("global"),session.NewButtonId);
            proposal=session.PreviewAt(0,0);copy.CommitDrop("global",proposal,null,null);
            copy.SetAppearance("global",proposal.Entries[0].Id,"改名",null);
            Check(Near(before,new MultiRingLayout(copy.Find("global")).Rotation(0)),
                "Same-ring reordering and property changes preserve rotation");
            var outer=copy.AddRing("global");for(var i=0;i<3;i++)copy.AddButton("global",outer);
            copy.RotateRing("global",outer,1);session=new(copy.Find("global"),copy.Find("global").Entries.First().Id);
            proposal=session.PreviewAt(outer,1);copy.CommitDrop("global",proposal,null,null);
            Check(Cardinal(copy.Find("global"),0)&&Cardinal(copy.Find("global"),outer),
                "Cross-ring moves align both rings whose button counts changed");

            Load(DefaultProfiles.Configuration(),false);await Task.Delay(180);
            rings.IsDropDownOpen=true;await Task.Delay(100);
            rings.SelectedItem=rings.Items.Cast<ComboBoxItem>().Single(i=>(int)i.Tag==-1);
            for(var i=0;i<20&&(addingRing||draft.Find(profileId).RingCount==1);i++)await Task.Delay(50);
            Check(draft.Find(profileId).RingCount==2&&(int)((ComboBoxItem)rings.SelectedItem).Tag==1&&!addingRing,
                "The centered circle dropdown adds and selects an outer ring without a XAML exception");
            draft.AddButton(profileId,1);RefreshPreview();menuSize.Value=75;await Task.Delay(180);
            Check(Near(draft.Find(profileId).SizeScale,0.75)&&Math.Abs(previewBox.Width-408)<0.5,
                "Size slider immediately renders a two-ring menu at about 408 DIP, allowing native pixel rounding");

            drag=new(draft.Find(profileId),draft.Find(profileId).Entries.First().Id);moved=true;
            var point=preview.TransformToVisual(XamlRoot.Content).TransformPoint(new(preview.Diameter/2+100,preview.Diameter/2));
            UpdateDropFromWindow(point,force:true);
            var hovered=dropProposal;
            for(var i=0;i<12;i++){UpdateDropFromWindow(point);await Task.Delay(20);}
            Check(hovered is not null&&dropProposal is not null&&hovered.Entries.SequenceEqual(dropProposal.Entries),
                "A stationary drag at the resized preview keeps the same insertion target");
            CancelDrag();
            var registry=new ActionRegistry();var ringWindow=new RingWindow();
            try
            {
                foreach(var percent in new[]{50,125,200})
                {
                    menuSize.Value=percent;await Task.Delay(100);
                    var p=draft.Find(profileId);ringWindow.SetMenu(p,registry);
                    var appWindow=Microsoft.UI.Windowing.AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(OwnerWindowHandle));
                    var x=appWindow.Position.X+appWindow.Size.Width/2;var y=appWindow.Position.Y+appWindow.Size.Height/2;
                    ringWindow.ShowAt(x,y);await Task.Delay(150);
                    var placement=ringWindow.PlacementForSmoke;
                    var transform=preview.TransformToVisual(previewBox);
                    var start=transform.TransformPoint(new(0,0));var end=transform.TransformPoint(new(preview.Diameter,0));
                    var actualDip=ringWindow.ViewForSmoke.Diameter*placement.Scale/OverlayWindow.ScaleAt(x,y);
                    var expected=p.Entries.First(e=>e.Ring==0).Id;
                    Check(Math.Abs(end.X-start.X-actualDip)<0.01&&
                        ringWindow.HitTest((int)Math.Round(placement.CenterX),(int)Math.Round(placement.CenterY-100*placement.Scale))==expected,
                        $"At {percent}%, the editor matches the native overlay diameter and runtime hit targets");
                    ringWindow.HideRing();
                }
            }
            finally{ringWindow.Close();}
            var roundtrip=ConfigurationCodec.Deserialize(ConfigurationCodec.Serialize(draft.Snapshot()));
            var document=MenuDocumentCodec.Deserialize(MenuDocumentCodec.Serialize(draft.ExportMenu(profileId)));
            var legacy=System.Text.Json.Nodes.JsonNode.Parse(ConfigurationCodec.Serialize(draft.Snapshot()))!;
            foreach(var node in legacy["profiles"]!.AsArray())node!.AsObject().Remove("sizeScale");
            Check(roundtrip.Profiles.Single(p=>p.Id==profileId).SizeScale==2&&document.Menu.SizeScale==2&&
                ConfigurationCodec.Deserialize(legacy.ToJsonString()).Profiles.All(p=>p.SizeScale==1),
                "Full and single-menu files preserve size; older files retain the original 100% size");
            var invalid=draft.Snapshot() with{Profiles=draft.Profiles.Select(p=>p with{SizeScale=0}).ToArray()};
            var rejected=false;try{ConfigurationCodec.Validate(invalid);}catch(ArgumentException){rejected=true;}
            Check(rejected,"Invalid imported size is rejected before applying the configuration");

            // Finish with the normal one-ring view for the focused visual review.
            Load(DefaultProfiles.Configuration(),false);await Task.Delay(180);
            var area=(Grid)previewScroll.Parent;
            var rotations=area.Children.OfType<StackPanel>().Where(p=>p.VerticalAlignment==VerticalAlignment.Top&&p.Children[0] is Button).ToArray();
            Check(rotations.Length==2&&rotations.All(p=>p.Children[0] is Button {Content:Microsoft.UI.Xaml.Shapes.Path})&&
                Grid.GetRow((FrameworkElement)savePreset.Parent)==2&&Grid.GetRow((FrameworkElement)trash.Parent)==2&&
                Math.Abs(area.ActualWidth/2-NearCenter((FrameworkElement)rings.Parent,area))<1,
                "Ring icons and captions occupy the upper corners, symmetric to preset/trash, with a centered circle selector");
            await SmokeScenario.RenderAsync((FrameworkElement)Content,Path.Combine(directory,"layout-editor.png"));
        }
        finally{Load(original,false);}
        static double NearCenter(FrameworkElement element,FrameworkElement parent)=>
            element.TransformToVisual(parent).TransformPoint(new Point(element.ActualWidth/2,0)).X;
    }
}
#endif