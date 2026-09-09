using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using MouseNavigator.Contracts;
using MouseNavigator.Core;
using Windows.System;
using Windows.UI.Core;

namespace MouseNavigator.App;

/// <summary>Visual editor over a detached draft. File dialogs and runtime composition stay in the host.</summary>
internal sealed class MenuEditor : UserControl
{
    private readonly IReadOnlyList<ActionDescriptor> actions;
    private ConfigurationDraft draft;
    private NavigatorConfiguration saved;
    private string profileId;
    private RingSlot selectedSlot = RingSlot.Top;
    private bool loading, recording;
    private readonly ComboBox profiles = new() { MinWidth = 240, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox name = new() { Header = "菜单名称", MaxLength = 80 };
    private readonly TextBox processes = new() { Header = "应用进程名（多个用分号分隔）", PlaceholderText = "例如：Code.exe; devenv.exe" };
    private readonly NumberBox priority = new() { Header = "匹配优先级", Minimum = int.MinValue, Maximum = int.MaxValue, SmallChange = 1, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
    private readonly TextBlock directionTitle = new() { FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    private readonly ComboBox actionChoice = new() { Header = "执行动作", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly StackPanel shortcutPanel = new() { Spacing = 12 };
    private readonly TextBox shortcutName = new() { Header = "显示名称", MaxLength = 80, PlaceholderText = "例如：复制、保存文件" };
    private readonly CheckBox ctrl = new() { Content = "Ctrl" }, alt = new() { Content = "Alt" }, shift = new() { Content = "Shift" }, win = new() { Content = "Win" };
    private readonly ComboBox mainKey = new() { Header = "主键", MinWidth = 160, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Button record = new() { Content = "录入快捷键", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock chord = new() { FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly Dictionary<RingSlot, Button> slots = [];
    private readonly Button delete = new() { Content = "删除菜单" };
    private readonly Button save = new() { Content = "保存并应用" };
    private readonly TextBlock dirtyText = new() { VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 };
    private readonly InfoBar feedback = new() { IsClosable = true };
    private readonly StackPanel root = new() { Spacing = 20 };
    public Func<NavigatorConfiguration, Task>? SaveRequested { get; set; }
    public Func<Task<NavigatorConfiguration?>>? ImportRequested { get; set; }
    public Func<NavigatorConfiguration, Task>? ExportRequested { get; set; }
    public bool IsDirty { get; private set; }

    public MenuEditor(NavigatorConfiguration configuration, IReadOnlyList<ActionDescriptor> availableActions)
    {
        saved = configuration;
        draft = new(configuration);
        profileId = configuration.Profiles.Single(p => p.Applications.Count == 0).Id;
        actions = availableActions;
        Content = root;
        root.Children.Add(new TextBlock { Text = "设计你的应用菜单", FontSize = 30, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        root.Children.Add(new TextBlock { Text = "选择菜单，点击方向，为常用操作安排一个位置。", Opacity = 0.7, TextWrapping = TextWrapping.Wrap });
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        toolbar.Children.Add(profiles);
        toolbar.Children.Add(MakeButton("新增应用", (_, _) => { profileId = draft.Add(); RefreshProfiles(); MarkDirty(); }));
        delete.Click += (_, _) => { draft.Delete(profileId); profileId = draft.Profiles.Single(p => p.Applications.Count == 0).Id; RefreshProfiles(); MarkDirty(); };
        toolbar.Children.Add(delete);
        toolbar.Children.Add(save);
        root.Children.Add(toolbar);

        var metadata = new Grid { ColumnSpacing = 12 };
        metadata.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        metadata.ColumnDefinitions.Add(new() { Width = new GridLength(1.5, GridUnitType.Star) });
        metadata.ColumnDefinitions.Add(new() { Width = new GridLength(120) });
        metadata.Children.Add(name); Grid.SetColumn(processes, 1); metadata.Children.Add(processes);
        Grid.SetColumn(priority, 2); metadata.Children.Add(priority);
        root.Children.Add(metadata);

        var editor = new Grid { ColumnSpacing = 24 };
        editor.ColumnDefinitions.Add(new() { Width = new GridLength(300) });
        editor.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        var previewPanel = new StackPanel { Spacing = 12 };
        previewPanel.Children.Add(new TextBlock { Text = "菜单预览", FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        previewPanel.Children.Add(BuildPreview());
        previewPanel.Children.Add(new TextBlock { Text = "点击方向编辑 · 中心为取消区域", HorizontalAlignment = HorizontalAlignment.Center, Opacity = 0.65, FontSize = 12 });
        editor.Children.Add(previewPanel);

        var settings = new StackPanel { Spacing = 14 };
        settings.Children.Add(directionTitle);
        settings.Children.Add(actionChoice);
        shortcutPanel.Children.Add(shortcutName);
        shortcutPanel.Children.Add(record);
        var modifiers = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var check in new[] { ctrl, alt, shift, win }) modifiers.Children.Add(check);
        shortcutPanel.Children.Add(modifiers);
        shortcutPanel.Children.Add(mainKey);
        shortcutPanel.Children.Add(chord);
        shortcutPanel.Children.Add(new TextBlock { Text = "可录入组合键，也可勾选修饰键并选择主键。快捷键发送给唤出菜单时的应用。", Opacity = 0.65, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        settings.Children.Add(shortcutPanel);
        var settingsCard = new Border { Padding = new Thickness(20), CornerRadius = new CornerRadius(16), Background = Brush(35, 40, 51), Child = settings };
        Grid.SetColumn(settingsCard, 1); editor.Children.Add(settingsCard);
        root.Children.Add(editor);

        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        save.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        save.Click += async (_, _) => await RunAsync(SaveDraftAsync);
        footer.Children.Add(MakeButton("放弃修改", (_, _) => Load(saved, false)));
        footer.Children.Add(MakeButton("恢复默认", (_, _) => { Load(DefaultProfiles.Configuration(), true); Notify("默认菜单已载入草稿，保存后生效。"); }));
        footer.Children.Add(dirtyText);
        root.Children.Add(footer);
        var files = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        files.Children.Add(MakeButton("导入 JSON", async (_, _) => await RunAsync(async () =>
        {
            var imported = ImportRequested is null ? null : await ImportRequested();
            if (imported is null) return;
            Load(imported, true);
            Notify("已导入为草稿，检查各应用与方向后，点击保存并应用。");
        })));
        files.Children.Add(MakeButton("导出 JSON", async (_, _) => await RunAsync(async () =>
        {
            if (ExportRequested is not null) await ExportRequested(draft.Snapshot());
        })));
        files.Children.Add(new TextBlock { Text = "导入替换当前草稿；导出包含全部菜单和快捷键。", VerticalAlignment = VerticalAlignment.Center, Opacity = 0.65, FontSize = 12 });
        root.Children.Add(files);
        root.Children.Add(feedback);

        PopulateActionChoices();
        foreach (var pair in ShortcutKeys.MainKeys) mainKey.Items.Add(new ComboBoxItem { Content = pair.Value, Tag = pair.Key });
        profiles.SelectionChanged += (_, _) =>
        {
            if (loading || profiles.SelectedItem is not ComboBoxItem item) return;
            profileId = (string)item.Tag;
            RefreshProfile();
        };
        name.TextChanged += (_, _) => UpdateMetadata();
        processes.TextChanged += (_, _) => UpdateMetadata();
        priority.ValueChanged += (_, _) => UpdateMetadata();
        actionChoice.SelectionChanged += (_, _) => ChooseAction();
        shortcutName.TextChanged += (_, _) => UpdateShortcut();
        foreach (var check in new[] { ctrl, alt, shift, win })
        {
            check.Checked += (_, _) => UpdateShortcut();
            check.Unchecked += (_, _) => UpdateShortcut();
        }
        mainKey.SelectionChanged += (_, _) => UpdateShortcut();
        record.Click += (_, _) => { recording = !recording; record.Content = recording ? "请按组合键（Esc 退出录入）" : "录入快捷键"; };
        record.LostFocus += (_, _) => StopRecording();
        record.PreviewKeyDown += RecordKey;
        RefreshProfiles();
        UpdateDirty();
    }

    private Grid BuildPreview()
    {
        var grid = new Grid { Width = 300, Height = 300 };
        grid.Children.Add(new Ellipse { Fill = Brush(28, 32, 42), Stroke = Brush(61, 69, 87), StrokeThickness = 1 });
        var center = new Border { Width = 76, Height = 76, CornerRadius = new CornerRadius(38), Background = Brush(44, 51, 66), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = "中键\n取消", TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        grid.Children.Add(center);
        foreach (var slot in Enum.GetValues<RingSlot>())
        {
            var button = new Button { Width = 100, Height = 82, Padding = new Thickness(5), CornerRadius = new CornerRadius(20), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            button.Margin = slot switch { RingSlot.Top => new(100, 5, 0, 0), RingSlot.Right => new(195, 109, 0, 0), RingSlot.Bottom => new(100, 213, 0, 0), _ => new(5, 109, 0, 0) };
            button.Click += (_, _) => { selectedSlot = slot; RefreshSlot(); };
            slots.Add(slot, button);
            grid.Children.Add(button);
        }
        return grid;
    }
    private void RefreshProfiles()
    {
        loading = true;
        profiles.Items.Clear();
        foreach (var profile in draft.Profiles)
        {
            var item = new ComboBoxItem { Content = profile.Name, Tag = profile.Id };
            profiles.Items.Add(item);
            if (profile.Id == profileId) profiles.SelectedItem = item;
        }
        loading = false;
        RefreshProfile();
    }
    private void RefreshProfile()
    {
        loading = true;
        var profile = draft.Find(profileId);
        name.Text = profile.Name;
        processes.IsEnabled = profile.Applications.Count != 0;
        processes.Text = profile.Applications.Count == 0 ? "全局回退 · 所有未单独配置的应用" : string.Join("; ", profile.Applications.Select(a => a.ProcessName));
        priority.Value = profile.Priority;
        delete.IsEnabled = profile.Applications.Count != 0;
        loading = false;
        RefreshSlot();
    }
    private void PopulateActionChoices()
    {
        var wasLoading = loading;
        loading = true;
        try
        {
            // Called only when a document is loaded, never inside SelectionChanged.
            actionChoice.IsDropDownOpen = false;
            actionChoice.Items.Clear();
            actionChoice.Items.Add(new ComboBoxItem { Content = "不执行动作", Tag = "" });
            foreach (var action in actions)
                actionChoice.Items.Add(new ComboBoxItem { Content = action.Name, Tag = action.Id });
            actionChoice.Items.Add(new ComboBoxItem { Content = "自定义快捷键…", Tag = "$shortcut" });
            foreach (var id in draft.Profiles.SelectMany(p => p.Entries).Select(e => e.ActionId).Distinct(StringComparer.Ordinal)
                .Where(id => draft.Shortcut(id) is null && actions.All(a => a.Id != id)))
                actionChoice.Items.Add(new ComboBoxItem { Content = "动作不可用：" + id, Tag = id });
        }
        finally { loading = wasLoading; }
    }
    private void RefreshSlot()
    {
        var wasLoading = loading;
        loading = true;
        try
        {
            var entry = draft.Find(profileId).Entries.FirstOrDefault(e => e.Slot == selectedSlot);
            var selected = draft.Shortcut(entry?.ActionId) is not null ? "$shortcut" : entry?.ActionId ?? "";
            directionTitle.Text = Direction(selectedSlot) + " · 设置动作";
            actionChoice.SelectedItem = actionChoice.Items.Cast<ComboBoxItem>().Single(i => (string)i.Tag == selected);
            RefreshShortcutFields();
        }
        finally { loading = wasLoading; }
    }
    private void RefreshShortcutFields()
    {
        StopRecording();
        var wasLoading = loading;
        loading = true;
        try
        {
            var entry = draft.Find(profileId).Entries.FirstOrDefault(e => e.Slot == selectedSlot);
            var shortcut = draft.Shortcut(entry?.ActionId);
            shortcutPanel.Visibility = shortcut is null ? Visibility.Collapsed : Visibility.Visible;
            shortcutName.Text = shortcut?.Name ?? "自定义快捷键";
            var keys = shortcut?.Keys ?? new ushort[] { 0x11, 0x4B };
            ctrl.IsChecked = keys.Contains((ushort)0x11); alt.IsChecked = keys.Contains((ushort)0x12);
            shift.IsChecked = keys.Contains((ushort)0x10); win.IsChecked = keys.Contains((ushort)0x5B);
            mainKey.SelectedItem = mainKey.Items.Cast<ComboBoxItem>().First(i => (ushort)i.Tag == keys[^1]);
            chord.Text = ShortcutKeys.Format(keys);
        }
        finally { loading = wasLoading; }
        RefreshPreview();
    }
    private void RefreshPreview()
    {
        foreach (var (slot, button) in slots)
        {
            var entry = draft.Find(profileId).Entries.FirstOrDefault(e => e.Slot == slot);
            var label = draft.Shortcut(entry?.ActionId)?.Name ?? actions.FirstOrDefault(a => a.Id == entry?.ActionId)?.Name ?? (entry is null ? "未配置" : "动作不可用");
            var stack = new StackPanel { Spacing = 5 };
            stack.Children.Add(new TextBlock { Text = Direction(slot), FontSize = 12, Opacity = 0.7, HorizontalAlignment = HorizontalAlignment.Center });
            stack.Children.Add(new TextBlock { Text = label, FontSize = 12, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxLines = 2 });
            button.Content = stack;
            button.Background = slot == selectedSlot ? Brush(48, 84, 139) : Brush(39, 45, 58);
            button.BorderBrush = slot == selectedSlot ? Brush(132, 177, 245) : Brush(57, 66, 84);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, $"{Direction(slot)}：{label}");
        }
    }
    private void UpdateMetadata()
    {
        if (loading) return;
        var current = draft.Find(profileId);
        var matches = current.Applications.Count == 0 ? current.Applications
            : processes.Text.Split([';', '；', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(p => new ApplicationMatch(p)).ToArray();
        // Retain an invalid draft rather than silently turning an application menu into a fallback.
        if (current.Applications.Count != 0 && matches.Count == 0) matches = new[] { new ApplicationMatch("") };
        draft.Update(profileId, name.Text, matches, double.IsNaN(priority.Value) ? 0 : (int)priority.Value);
        if (profiles.SelectedItem is ComboBoxItem item) item.Content = name.Text;
        MarkDirty();
    }
    private void ChooseAction()
    {
        if (loading || actionChoice.SelectedItem is not ComboBoxItem item) return;
        var id = (string)item.Tag;
        if (id == "$shortcut") draft.SetShortcut(profileId, selectedSlot, "自定义快捷键", new ushort[] { 0x11, 0x4B });
        else draft.SetAction(profileId, selectedSlot, id.Length == 0 ? null : id);
        // Keep the selected ComboBoxItem alive until WinUI finishes processing the selection.
        MarkDirty(); RefreshShortcutFields();
    }
    private void UpdateShortcut()
    {
        if (loading || shortcutPanel.Visibility != Visibility.Visible || mainKey.SelectedItem is not ComboBoxItem item) return;
        var keys = new List<ushort>();
        if (ctrl.IsChecked == true) keys.Add(0x11); if (alt.IsChecked == true) keys.Add(0x12);
        if (shift.IsChecked == true) keys.Add(0x10); if (win.IsChecked == true) keys.Add(0x5B);
        keys.Add((ushort)item.Tag);
        draft.SetShortcut(profileId, selectedSlot, shortcutName.Text, keys);
        chord.Text = ShortcutKeys.Format(keys);
        MarkDirty(); RefreshPreview();
    }
    private void RecordKey(object sender, KeyRoutedEventArgs e)
    {
        if (!recording) return;
        e.Handled = true;
        if (e.Key == VirtualKey.Escape) { StopRecording(); return; }
        var key = (ushort)e.Key;
        if (!ShortcutKeys.MainKeys.ContainsKey(key)) return;
        bool Down(VirtualKey k) => (InputKeyboardSource.GetKeyStateForCurrentThread(k) & CoreVirtualKeyStates.Down) != 0;
        loading = true;
        ctrl.IsChecked = Down(VirtualKey.Control); alt.IsChecked = Down(VirtualKey.Menu);
        shift.IsChecked = Down(VirtualKey.Shift); win.IsChecked = Down(VirtualKey.LeftWindows) || Down(VirtualKey.RightWindows);
        mainKey.SelectedItem = mainKey.Items.Cast<ComboBoxItem>().Single(i => (ushort)i.Tag == key);
        loading = false;
        StopRecording(); UpdateShortcut();
    }
    private void StopRecording() { recording = false; record.Content = "录入快捷键"; }
    private void Load(NavigatorConfiguration configuration, bool dirty)
    {
        draft = new(configuration);
        profileId = draft.Profiles.Single(p => p.Applications.Count == 0).Id;
        PopulateActionChoices();
        IsDirty = dirty; RefreshProfiles(); UpdateDirty(); feedback.IsOpen = false;
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
    private void MarkDirty() { IsDirty = true; UpdateDirty(); }
    private void UpdateDirty() { save.IsEnabled = IsDirty; dirtyText.Text = IsDirty ? "有未保存修改" : "已与当前配置同步"; }
    public void Notify(string message, InfoBarSeverity severity = InfoBarSeverity.Informational)
    {
        feedback.Message = message; feedback.Severity = severity; feedback.IsOpen = true;
        DispatcherQueue.TryEnqueue(() => { if (feedback.IsLoaded) feedback.StartBringIntoView(); });
    }
    private async Task RunAsync(Func<Task> action)
    {
        StopRecording();
        IsEnabled = false;
        try { await action(); }
        catch (Exception ex) { Notify(ex.Message, InfoBarSeverity.Error); }
        finally { IsEnabled = true; }
    }
    private static string Direction(RingSlot slot) => slot switch { RingSlot.Top => "上方", RingSlot.Right => "右侧", RingSlot.Bottom => "下方", _ => "左侧" };
    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(ColorHelper.FromArgb(255, r, g, b));
    private static Button MakeButton(string label, RoutedEventHandler clicked)
    { var button = new Button { Content = label }; button.Click += clicked; return button; }

#if DEBUG
    internal async Task CheckOpenSelectionsForSmokeAsync(List<string> results)
    {
        var stableActions = true;
        foreach (var id in new[] { "windows.maximize", "$shortcut", "", "windows.maximize", "$shortcut", "windows.tasks" })
        {
            stableActions &= await SelectOpenItem(actionChoice, id);
            var entry = draft.Find(profileId).Entries.FirstOrDefault(e => e.Slot == selectedSlot);
            stableActions &= id == "$shortcut" ? draft.Shortcut(entry?.ActionId) is not null
                : (entry?.ActionId ?? "") == id;
        }
        results.Add((stableActions ? "PASS: " : "FAIL: ") + "Open action dropdown retains its selected container and applies all choices");

        var before = ConfigurationCodec.Serialize(draft.Snapshot());
        var stableProfiles = true;
        foreach (var id in draft.Profiles.Select(p => p.Id).Reverse().ToArray())
            stableProfiles &= await SelectOpenItem(profiles, id);
        stableProfiles &= before == ConfigurationCodec.Serialize(draft.Snapshot());
        results.Add((stableProfiles ? "PASS: " : "FAIL: ") + "Open profile dropdown switches without changing configuration data");
    }
    private static async Task<bool> SelectOpenItem(ComboBox combo, string id)
    {
        combo.IsDropDownOpen = true;
        await Task.Delay(120);
        var item = combo.Items.Cast<ComboBoxItem>().Single(i => (string)i.Tag == id);
        combo.SelectedItem = item;
        combo.IsDropDownOpen = false;
        await Task.Delay(120);
        return ReferenceEquals(combo.SelectedItem, item);
    }
    internal NavigatorConfiguration DraftForSmoke => draft.Snapshot();
    internal void EditShortcutForSmoke()
    {
        selectedSlot = RingSlot.Right; RefreshSlot();
        actionChoice.SelectedItem = actionChoice.Items.Cast<ComboBoxItem>().Single(i => (string)i.Tag == "$shortcut");
        shortcutName.Text = "保存文件";
        mainKey.SelectedItem = mainKey.Items.Cast<ComboBoxItem>().Single(i => (ushort)i.Tag == 0x53);
    }
#endif
}