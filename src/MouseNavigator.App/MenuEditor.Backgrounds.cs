using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MouseNavigator.Contracts;

namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    private static readonly Lazy<string> sampleBackground=new(()=>{
        using var stream=typeof(MenuEditor).Assembly.GetManifestResourceStream("MouseNavigator.App.Assets.halo.png")!;
        using var bytes=new MemoryStream();stream.CopyTo(bytes);return Convert.ToBase64String(bytes.ToArray());
    });
    private ComboBox BackgroundPicker(string? current,Action<string?> apply,int generation,string targetId)
    {
        var picker=new ComboBox{Header="图片预设",HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch};
        var rebuilding=false;var busy=false;var deleteButtons=new List<Button>();
        bool Current()=>generation==appearanceGeneration&&profileId==targetId;
        void Tools(bool visible){foreach(var button in deleteButtons)button.Visibility=visible?Visibility.Visible:Visibility.Collapsed;}
        void Rebuild()
        {
            rebuilding=true;
            try
            {
                picker.Items.Clear();deleteButtons.Clear();
                var empty=new ComboBoxItem{Content="无图片",Tag=""};picker.Items.Add(empty);
                ComboBoxItem selected=empty;
                void Add(string id,string name,string data,bool stored)
                {
                    var row=new Grid{ColumnSpacing=8,MinWidth=170};
                    row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
                    row.Children.Add(new TextBlock{Text=name,MaxWidth=150,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center});
                    var remove=new Button{Content=new FontIcon{Glyph="\uE74D",FontSize=14},Padding=new Thickness(5),Visibility=Visibility.Collapsed};
                    ToolTipService.SetToolTip(remove,"删除图片预设");
                    Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(remove,"删除 "+name);
                    Grid.SetColumn(remove,1);row.Children.Add(remove);deleteButtons.Add(remove);
                    var item=new ComboBoxItem{Content=row,Tag=data,HorizontalContentAlignment=HorizontalAlignment.Stretch};
                    remove.Click+=(_,_)=>{
                        picker.IsDropDownOpen=false;
                        DispatcherQueue.TryEnqueue(()=>{
                            if(!Current())return;
                            if(stored){draft.RemoveBackgroundPreset(id);MarkDirty();}
                            else{current=null;apply(null);}
                            Rebuild();
                        });
                    };
                    picker.Items.Add(item);if(data==current)selected=item;
                }
                foreach(var preset in draft.BackgroundPresets)
                    Add(preset.Id,preset.Id=="builtin.mandala"?"静谧光环":preset.Name,preset.Image??sampleBackground.Value,true);
                if(current is not null&&selected==empty)Add("$current","当前图片",current,false);
                picker.Items.Add(new ComboBoxItem{Content="+ 导入图片…",Tag="$import"});
                picker.SelectedItem=selected;
            }
            finally{rebuilding=false;}
        }
        picker.SelectionChanged+=(_,_)=>{
            if(rebuilding||busy||!Current()||picker.SelectedItem is not ComboBoxItem item)return;
            if(Equals(item.Tag,"$import"))
            {
                busy=true;picker.IsDropDownOpen=false;
                DispatcherQueue.TryEnqueue(async()=>{
                    try
                    {
                        // Let ComboBox finish committing its selected container and closing its popup.
                        // Never clear/rebuild Items from inside SelectionChanged.
                        await Task.Delay(180);
                        if(!Current())return;
                        rebuilding=true;
                        try{picker.SelectedItem=picker.Items.Cast<ComboBoxItem>().FirstOrDefault(i=>Equals(i.Tag,current??""))??picker.Items[0];}
                        finally{rebuilding=false;}
                        if(dialogOpen)return;
                        dialogOpen=true;picker.IsEnabled=false;
                        string? data;
                        try{data=PickBackgroundRequested is null?null:await PickBackgroundRequested();}
                        finally{dialogOpen=false;picker.IsEnabled=true;}
                        if(data is null||!Current())return;
                        draft.AddBackgroundPreset(data);current=data;apply(data);Rebuild();
                    }
                    catch(Exception ex){Notify(ex.Message,InfoBarSeverity.Error);}
                    finally{busy=false;}
                });
                return;
            }
            current=Equals(item.Tag,"")?null:(string)item.Tag;apply(current);
        };
        picker.DropDownOpened+=(_,_)=>Tools(true);
        picker.DropDownClosed+=(_,_)=>DispatcherQueue.TryEnqueue(()=>{if(!picker.IsDropDownOpen)Tools(false);});
        Rebuild();return picker;
    }
}
