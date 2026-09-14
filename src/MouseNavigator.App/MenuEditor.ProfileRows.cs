using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MouseNavigator.Contracts;
using System.ComponentModel;
using System.Windows.Input;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    private sealed class RowCommand(Action execute,bool enabled=true):ICommand
    {
        public bool CanExecute(object? parameter)=>enabled;
        public void Execute(object? parameter)=>execute();
        public event EventHandler? CanExecuteChanged {add{} remove{}}
    }
    private sealed class ProfileChoice:INotifyPropertyChanged
    {
        public required string Name{get;set;}
        public void SetName(string name){Name=name;PropertyChanged?.Invoke(this,new(nameof(Name)));}
        public required ICommand Rename{get;init;}
        public required ICommand Delete{get;set;}
        public void SetDelete(ICommand command){Delete=command;PropertyChanged?.Invoke(this,new(nameof(Delete)));}
        private Visibility visibility=Visibility.Collapsed;
        public Visibility ToolsVisibility {get=>visibility;set{visibility=value;PropertyChanged?.Invoke(this,new(nameof(ToolsVisibility)));}}
        public event PropertyChangedEventHandler? PropertyChanged;
        public override string ToString()=>Name;
    }
    private readonly List<ProfileChoice> profileRowTools=[];
    private readonly DataTemplate profileRowTemplate=(DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
          <Grid MinWidth="280" ColumnSpacing="8">
            <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
            <TextBlock Text="{Binding Name}" VerticalAlignment="Center" TextTrimming="CharacterEllipsis" MaxWidth="240"/>
            <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="4" Visibility="{Binding ToolsVisibility}">
              <Button Width="30" Height="30" Padding="4" Command="{Binding Rename}" ToolTipService.ToolTip="重命名" AutomationProperties.Name="重命名菜单"><FontIcon Glyph="&#xE70F;" FontSize="14"/></Button>
              <Button Width="30" Height="30" Padding="4" Command="{Binding Delete}" ToolTipService.ToolTip="删除菜单" AutomationProperties.Name="删除菜单"><FontIcon Glyph="&#xE74D;" FontSize="14"/></Button>
            </StackPanel>
          </Grid>
        </DataTemplate>
        """);
    private ProfileChoice ProfileRow(MenuProfile profile)
    {
        var choice=new ProfileChoice{Name=profile.Name,Rename=new RowCommand(()=>QueueProfileOperation(profile.Id,false)),
            Delete=new RowCommand(()=>QueueProfileOperation(profile.Id,true),!profile.IsDefault)};
        profileRowTools.Add(choice);return choice;
    }
    private void SetProfileRowTools(bool show)
    {
        foreach(var row in profileRowTools)row.ToolsVisibility=show?Visibility.Visible:Visibility.Collapsed;
    }
    private int profileSelectionVersion;
    private bool profileSwitchPending;
    private void RestoreProfileSelection()
    {
        var prior=loading;loading=true;
        try{profiles.SelectedItem=profiles.Items.Cast<ComboBoxItem>().FirstOrDefault(i=>(string)i.Tag==profileId);}
        finally{loading=prior;}
    }
    private void QueueProfileSelection(string id)
    {
        if(id==profileId)return;
        if(profileSwitchPending||newMenuPending||dialogOpen){RestoreProfileSelection();return;}
        var version=++profileSelectionVersion;profileSwitchPending=true;
        profiles.IsDropDownOpen=false;
        DispatcherQueue.TryEnqueue(async()=>{
            try
            {
                if(version!=profileSelectionVersion||newMenuPending||loading)return;
                RestoreProfileSelection();
                if(!await ConfirmPendingChangesAsync("切换"))return;
                if(!draft.Profiles.Any(p=>p.Id==id))return;
                CancelDrag();profileId=id;selectedButtonId=null;RestoreProfileSelection();RefreshProfile();
            }
            catch(Exception ex){Notify(ex.Message,InfoBarSeverity.Error);}
            finally{RestoreProfileSelection();profileSwitchPending=false;}
        });
    }
    private void QueueProfileOperation(string id,bool remove)
    {
        if(dialogOpen||newMenuPending)return;
        newMenuPending=true;++profileSelectionVersion;
        IsTabStop=true;Focus(FocusState.Programmatic);
        profiles.IsDropDownOpen=false;
        DispatcherQueue.TryEnqueue(async()=>{
            try
            {
                CancelDrag();
                if(!draft.Profiles.Any(p=>p.Id==id))return;
                if(remove)
                {
                    if(await ConfirmAsync("删除菜单","删除“"+draft.Find(id).Name+"”？"))
                    {
                        loading=true;
                        try
                        {
                            if(profileId==id){profileId=draft.Profiles.Single(p=>p.IsDefault).Id;selectedButtonId=null;}
                            profiles.SelectedItem=profiles.Items.Cast<ComboBoxItem>().First(i=>(string)i.Tag==profileId);
                            draft.Delete(id);
                        }
                        finally{loading=false;}
                        MarkDirty();RefreshProfiles();
                    }
                }
                else {if(profileId!=id&&!await ConfirmPendingChangesAsync("切换"))return;if(!draft.Profiles.Any(p=>p.Id==id))return;profileId=id;selectedButtonId=null;RestoreProfileSelection();RefreshProfile();await RenameAsync(false);}
            }
            catch(Exception ex){Notify(ex.Message,InfoBarSeverity.Error);}
            finally{newMenuPending=false;}
        });
    }
}