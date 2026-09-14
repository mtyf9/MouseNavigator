using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    private string? currentPresetFolder;
    private bool presetNavigationPending;
    private readonly TextBlock presetBreadcrumb=new(){Text="预设按钮",FontSize=12,TextWrapping=TextWrapping.Wrap};
    private readonly Button presetBack=new(){Content="返回上级",IsEnabled=false};
    private string? WritablePresetFolder=>currentPresetFolder?.StartsWith("$")==true?null:currentPresetFolder;
    private string FolderPath(string? id)
    {
        if(id is null)return "预设按钮";
        var names=new List<string>();var seen=new HashSet<string>();
        while(id is not null&&seen.Add(id))
        {
            var folder=draft.PresetFolders.FirstOrDefault(f=>f.Id==id);if(folder is null)break;
            names.Insert(0,folder.Name);id=folder.ParentId;
        }
        return string.Join(" / ",names);
    }
    private void NavigatePresetFolder(string? id)
    {
        if(presetNavigationPending)return;presetNavigationPending=true;
        DispatcherQueue.TryEnqueue(()=>{
            try{currentPresetFolder=id;presetSearch.Text="";RefreshPresets();}
            catch(Exception ex){Notify(ex.Message,InfoBarSeverity.Error);}
            finally{presetNavigationPending=false;}
        });
    }
    private void RefreshPresetFolders()
    {
        if(currentPresetFolder is not null&&!currentPresetFolder.StartsWith("$")&&!draft.PresetFolders.Any(f=>f.Id==currentPresetFolder))currentPresetFolder=null;
        presetBack.IsEnabled=currentPresetFolder is not null;
        presetBreadcrumb.Text=currentPresetFolder is null?"预设按钮":currentPresetFolder.StartsWith("$category:")?"内置 / "+currentPresetFolder[10..]:FolderPath(currentPresetFolder);
    }
    private void AddFolderCards()
    {
        void Add(string title,string id)
        {
            var query=presetSearch.Text.Trim();if(query.Length>0&&!title.Contains(query,StringComparison.OrdinalIgnoreCase))return;
            var content=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10};
            content.Children.Add(ButtonIcons.Create("\uE8B7",null,24));
            content.Children.Add(new TextBlock{Text=title,TextWrapping=TextWrapping.Wrap,MaxWidth=140,VerticalAlignment=VerticalAlignment.Center});
            var button=new Button{Content=content,Tag=id,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Left,MinHeight=60,Background=presetThemeBrush};

            button.Resources["ButtonBackgroundPointerOver"]=presetThemeBrush;
            button.Resources["ButtonBackgroundPressed"]=presetThemeBrush;
            button.Resources["ButtonBorderBrushPointerOver"]=new Microsoft.UI.Xaml.Media.SolidColorBrush(PreviewPalette.Accent);
            button.Click+=(_,_)=>NavigatePresetFolder(id);presetCards.Children.Add(button);
        }
        if(currentPresetFolder is null)
            foreach(var category in actions.Select(a=>a.Category).Append("基础").Distinct())Add(category,"$category:"+category);
        if(currentPresetFolder?.StartsWith("$")!=true)
            foreach(var folder in draft.PresetFolders.Where(f=>f.ParentId==currentPresetFolder))Add(folder.Name,folder.Id);
    }
    private bool PresetInCurrentFolder(MouseNavigator.Contracts.ButtonPreset preset,bool builtin,string category)
    {
        if(preset.Id=="builtin-blank")return currentPresetFolder is null;
        if(currentPresetFolder?.StartsWith("$category:")==true)return builtin&&category==currentPresetFolder[10..];
        if(builtin)return currentPresetFolder is null&&(preset.Id=="builtin-blank"||presetSearch.Text.Trim().Length>0);
        if(presetSearch.Text.Trim().Length==0)return preset.FolderId==currentPresetFolder;
        var folder=preset.FolderId;
        while(folder!=currentPresetFolder&&folder is not null)folder=draft.PresetFolders.FirstOrDefault(f=>f.Id==folder)?.ParentId;
        return folder==currentPresetFolder;
    }
    private async Task CreatePresetFolderAsync()
    {
        if(dialogOpen)return;dialogOpen=true;
        var name=new TextBox{Header="文件夹名称",MaxLength=80};
        var dialog=new ContentDialog{Title="新建预设文件夹",Content=name,PrimaryButtonText="创建",CloseButtonText="取消",XamlRoot=XamlRoot};
        dialog.PrimaryButtonClick+=(_,args)=>{
            if(string.IsNullOrWhiteSpace(name.Text)||draft.PresetFolders.Any(f=>f.ParentId==WritablePresetFolder&&f.Name.Equals(name.Text.Trim(),StringComparison.OrdinalIgnoreCase))||draft.PresetFolders.Count>=100)
            {args.Cancel=true;name.Header="请输入不重复的名称（最多 100 个文件夹）";}
        };
        try
        {
            if(await ShowDialogAsync(dialog)!=ContentDialogResult.Primary)return;
            var id=draft.AddPresetFolder(name.Text,WritablePresetFolder);MarkDirty();RefreshPresets();
            NavigatePresetFolder(id);
        }
        finally{dialogOpen=false;}
    }
    private async Task SaveSelectedPresetAsync(string? id=null)
    {
        id??=selectedButtonId;if(id is null||id==RadialMenuView.CenterButtonId||dialogOpen)return;
        dialogOpen=true;
        try
        {
            var visual=preview.Buttons.Single(b=>b.Id==id);
            var name=new TextBox{Header="预设名称",Text=visual.Label,MaxLength=80};
            var location=new ComboBox{Header="存放位置",HorizontalAlignment=HorizontalAlignment.Stretch};
            location.Items.Add(new ComboBoxItem{Content="预设按钮",Tag=""});
            foreach(var folder in draft.PresetFolders)location.Items.Add(new ComboBoxItem{Content=FolderPath(folder.Id),Tag=folder.Id});
            var selected=WritablePresetFolder;
            location.SelectedItem=location.Items.Cast<ComboBoxItem>().FirstOrDefault(i=>(string)i.Tag==selected)??location.Items[0];
            var create=new TextBox{Header="或新建文件夹（可选）",MaxLength=80};
            var content=new StackPanel{Spacing=12};content.Children.Add(name);content.Children.Add(location);content.Children.Add(create);
            var dialog=new ContentDialog{Title="保存预设按钮",Content=content,PrimaryButtonText="保存",CloseButtonText="取消",XamlRoot=XamlRoot};
            dialog.PrimaryButtonClick+=(_,args)=>{
                if(string.IsNullOrWhiteSpace(name.Text)){args.Cancel=true;name.Header="请输入预设名称";}
                if(!string.IsNullOrWhiteSpace(create.Text)&&(draft.PresetFolders.Count>=100||draft.PresetFolders.Any(f=>f.ParentId==(((ComboBoxItem)location.SelectedItem).Tag as string is {Length:>0} parent?parent:null)&&f.Name.Equals(create.Text.Trim(),StringComparison.OrdinalIgnoreCase))))
                {args.Cancel=true;create.Header="名称已存在或文件夹数量已达上限";}
            };
            if(await ShowDialogAsync(dialog)!=ContentDialogResult.Primary)return;
            var folderId=string.IsNullOrWhiteSpace(create.Text)?(string)((ComboBoxItem)location.SelectedItem).Tag:draft.AddPresetFolder(create.Text,((string)((ComboBoxItem)location.SelectedItem).Tag) is {Length:>0} parent?parent:null);
            draft.SavePreset(profileId,id,name.Text.Trim(),visual.Glyph,folderId.Length==0?null:folderId);
            MarkDirty();RefreshPresets();Notify("已保存预设及其动作、宏和图标。");
        }
        catch(Exception ex){Notify(ex.Message,InfoBarSeverity.Error);}
        finally{dialogOpen=false;}
    }
}