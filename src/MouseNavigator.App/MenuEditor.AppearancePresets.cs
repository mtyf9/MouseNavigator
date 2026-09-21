using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MouseNavigator.Contracts;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    private async Task PersistAppearancePresetsAsync(IReadOnlyList<AppearancePreset> values)
    {
        if(SaveRequested is null)throw new InvalidOperationException("配置保存服务不可用。");
        var next=saved with{AppearancePresets=values.ToArray()};
        MouseNavigator.Core.ConfigurationCodec.Validate(next);
        await SaveRequested(next);
        saved=next;draft.ReplaceAppearancePresets(values);
        Notify("外观预设已保存。",InfoBarSeverity.Success);
    }
    private FrameworkElement CreateAppearancePresets()
    {
        var body=new StackPanel{Spacing=10};
        var saveCurrent=new Button{Content="保存当前外观为预设",HorizontalAlignment=HorizontalAlignment.Stretch};
        saveCurrent.Click+=async(_,_)=>await RunAsync(async()=>{
            if(dialogOpen)return;dialogOpen=true;
            var appearance=MenuAppearance.From(draft.Find(profileId));
            try
            {
                var target=new ComboBox{Header="保存到",HorizontalAlignment=HorizontalAlignment.Stretch};
                target.Items.Add(new ComboBoxItem{Content="+ 新建预设",Tag=""});
                foreach(var preset in draft.AppearancePresets)target.Items.Add(new ComboBoxItem{Content=preset.Name,Tag=preset.Id});
                target.SelectedIndex=0;
                var name=new TextBox{Header="预设名称",MaxLength=80};
                target.SelectionChanged+=(_,_)=>{name.Visibility=target.SelectedIndex==0?Visibility.Visible:Visibility.Collapsed;};
                var dialog=new ContentDialog{Title="保存外观预设",Content=new StackPanel{Spacing=12,Children={target,name}},PrimaryButtonText="保存",CloseButtonText="取消",XamlRoot=XamlRoot};
                dialog.PrimaryButtonClick+=(_,e)=>{
                    if(target.SelectedIndex!=0)return;
                    if(string.IsNullOrWhiteSpace(name.Text)||draft.AppearancePresets.Any(p=>p.Name.Equals(name.Text.Trim(),StringComparison.OrdinalIgnoreCase))||draft.AppearancePresets.Count>=50)
                    {e.Cancel=true;name.Header="请输入不重复的名称（最多 50 个预设）";}
                };
                if(await ShowDialogAsync(dialog)!=ContentDialogResult.Primary)return;
                var list=draft.AppearancePresets.ToList();var id=(string)((ComboBoxItem)target.SelectedItem).Tag;
                if(id.Length==0)list.Add(new("appearance-"+Guid.NewGuid().ToString("N"),name.Text.Trim(),appearance));
                else {var index=list.FindIndex(p=>p.Id==id);list[index]=list[index] with{Appearance=appearance};}
                await PersistAppearancePresetsAsync(list);BuildAppearanceSidebar();
            }
            finally{dialogOpen=false;}
        });
        body.Children.Add(saveCurrent);
        body.Children.Add(new TextBlock{Text="预设创建、覆盖和删除立即保存，切换菜单或放弃菜单修改不会丢失。应用预设后需保存悬浮窗配置。",TextWrapping=TextWrapping.Wrap,FontSize=12,Opacity=.7});
        foreach(var preset in draft.AppearancePresets)
        {
            var row=new Grid{ColumnSpacing=6};row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
            var use=new Button{Content=preset.Name,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Left};
            use.Click+=(_,_)=>{draft.ApplyAppearancePreset(profileId,preset.Id);MarkDirty();RefreshProfile();};
            var delete=new Button{Content=new FontIcon{Glyph="\uE74D",FontSize=14},Padding=new Thickness(8)};
            ToolTipService.SetToolTip(delete,"删除外观预设");
            delete.Click+=async(_,_)=>await RunAsync(async()=>{await PersistAppearancePresetsAsync(draft.AppearancePresets.Where(p=>p.Id!=preset.Id).ToArray());BuildAppearanceSidebar();});
            row.Children.Add(use);Grid.SetColumn(delete,1);row.Children.Add(delete);body.Children.Add(row);
        }
        return body;
    }
}
