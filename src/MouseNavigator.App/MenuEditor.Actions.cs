using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using Windows.System;
using Windows.UI.Core;
namespace MouseNavigator.App;
internal sealed partial class MenuEditor
{
    private void UpdateShortcut()
    {
        if (loading || selectedButtonId is null || shortcutPanel.Visibility != Visibility.Visible || mainKey.SelectedItem is not ComboBoxItem item) return;
        var keys = new List<ushort>();
        if (ctrl.IsChecked == true) keys.Add(0x11); if (alt.IsChecked == true) keys.Add(0x12);
        if (shift.IsChecked == true) keys.Add(0x10); if (win.IsChecked == true) keys.Add(0x5B);
        keys.Add((ushort)item.Tag);
        var current=draft.Shortcut(Entry?.ActionId);
        if(Entry is null||(current is not null&&current.Name==shortcutName.Text&&current.Keys.SequenceEqual(keys)))return;
        if(string.IsNullOrWhiteSpace(shortcutName.Text))return;
        draft.SetShortcut(profileId, selectedButtonId, shortcutName.Text, keys);
        chord.Text = ShortcutKeys.Format(keys);
        MarkDirty(); RefreshPreview();
    }
    public nint OwnerWindowHandle {get;set;}
    private MouseNavigator.Windows.ShortcutRecorder? shortcutRecorder;
    private readonly DispatcherTimer recordingTimeout=new(){Interval=TimeSpan.FromSeconds(20)};
    private int recordingSession;
    private void StartRecording()
    {
        if(recording){StopRecording();return;}
        StopRecording();var session=recordingSession;
        try
        {
            recording=true;record.Content="请按组合键后全部松开（Esc 取消）";
            shortcutRecorder=new(OwnerWindowHandle,keys=>DispatcherQueue.TryEnqueue(()=>
            {
                if(!recording||session!=recordingSession)return;
                StopRecording();if(keys is null||Entry is null)return;
                loading=true;
                try
                {
                    ctrl.IsChecked=keys.Contains((ushort)17);alt.IsChecked=keys.Contains((ushort)18);
                    shift.IsChecked=keys.Contains((ushort)16);win.IsChecked=keys.Contains((ushort)91);
                    mainKey.SelectedItem=mainKey.Items.Cast<ComboBoxItem>().Single(i=>(ushort)i.Tag==keys[^1]);
                }
                finally{loading=false;}
                UpdateShortcut();
            }));
            recordingTimeout.Start();
        }
        catch(Exception ex){StopRecording();Notify("无法录入快捷键："+ex.Message,InfoBarSeverity.Error);}
    }
    internal void StopRecording()
    {
        macroStopAction?.Invoke();
        recording=false;recordingSession++;recordingTimeout.Stop();
        shortcutRecorder?.Dispose();shortcutRecorder=null;record.Content="录入快捷键";
    }
    internal NavigatorConfiguration ConfigurationForBackup => draft.Snapshot();
    internal void AcceptRestoredConfiguration(NavigatorConfiguration configuration)
    {
        saved=configuration;Load(configuration,false);
    }
    private void Load(NavigatorConfiguration configuration, bool dirty)
    {
        CancelDrag();dirty|=!configuration.BuiltInMenusInitialized;configuration=DefaultProfiles.InitializeMenus(configuration);draft = new(configuration);
        profileId = draft.Profiles.Single(p => p.IsDefault).Id; selectedButtonId=null;
        PopulateActionChoices();
        IsDirty = dirty; RefreshPresets(); RefreshProfiles(); ClearNotification();UpdateDirty();
    }
    public async Task SaveDraftAsync()
    {
        var wasEnabled = IsEnabled;
        IsEnabled = false;
        try
        {
            var snapshot = draft.Snapshot();
            if (SaveRequested is null) throw new InvalidOperationException("配置保存服务不可用。");
            await SaveRequested(snapshot);
            saved = snapshot;
            IsDirty = false; UpdateDirty();
            Notify("已保存并应用，下次触发立即使用新菜单。", InfoBarSeverity.Success);
        }
        finally { IsEnabled = wasEnabled; }
    }
    private void MarkDirty()
    {
        IsDirty=true;
        if(notificationSeverity is InfoBarSeverity.Success or InfoBarSeverity.Informational)notificationMessage=null;
        UpdateDirty();
    }
    private void UpdateDirty(){save.IsEnabled=IsDirty;RenderNotification();}
    private string? notificationMessage;
    private InfoBarSeverity notificationSeverity=InfoBarSeverity.Informational;
    internal InfoBar NotificationBar=>feedback;
    private void RenderNotification()
    {
        feedback.Title=IsDirty?"有未保存修改":"已与当前配置同步";
        feedback.Message=notificationMessage??(IsDirty?"保存并应用后生效。":"");
        feedback.Severity=notificationMessage is null?InfoBarSeverity.Informational:notificationSeverity;
        feedback.IsClosable=notificationMessage is not null;
        feedback.IsOpen=true;
    }
    private void ClearNotification()
    {
        notificationMessage=null;notificationSeverity=InfoBarSeverity.Informational;RenderNotification();
    }
    public void Notify(string message,InfoBarSeverity severity=InfoBarSeverity.Informational,string? title=null)
    {
        notificationMessage=string.IsNullOrWhiteSpace(title)?message:title+"："+message;
        notificationSeverity=severity;RenderNotification();
    }
    private async Task RunAsync(Func<Task> action)
    {
        StopRecording();
        IsEnabled = false;
        try { await action(); }
        catch (Exception ex) { Notify(ex.Message, InfoBarSeverity.Error); }
        finally { IsEnabled = true; }
    }
    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(ColorHelper.FromArgb(255, r, g, b));
    private static Button MakeButton(string label, RoutedEventHandler clicked)
    { var button = new Button { Content = label }; button.Click += clicked; return button; }


}
