using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace MouseNavigator_WPF
{
    public partial class ButtonConfigWindow : Window
    {
        private FloatingBallConfig _config;
        private Dictionary<string, ComboBox> _actionComboBoxes;
        private Dictionary<string, TextBox> _shortcutTextBoxes;

        public FloatingBallConfig Config => _config;

        public ButtonConfigWindow(FloatingBallConfig config)
        {
            InitializeComponent();
            _config = new FloatingBallConfig();
            
            // 深拷贝配置
            foreach (var kvp in config.ButtonConfigs)
            {
                _config.ButtonConfigs[kvp.Key] = new ButtonConfiguration
                {
                    ButtonName = kvp.Value.ButtonName,
                    ActionType = kvp.Value.ActionType,
                    DisplayName = kvp.Value.DisplayName,
                    CustomShortcut = kvp.Value.CustomShortcut
                };
            }

            _actionComboBoxes = new Dictionary<string, ComboBox>();
            _shortcutTextBoxes = new Dictionary<string, TextBox>();
            
            InitializeUI();
        }

        private void InitializeUI()
        {
            var buttonNames = new[] { "Button0", "Button3", "Button6", "Button9", "Button12" };
            var buttonDisplayNames = new Dictionary<string, string>
            {
                { "Button0", "中心按钮" },
                { "Button3", "右侧按钮" },
                { "Button6", "底部按钮" },
                { "Button9", "左侧按钮" },
                { "Button12", "顶部按钮" }
            };

            foreach (var buttonName in buttonNames)
            {
                CreateButtonConfigUI(buttonName, buttonDisplayNames[buttonName]);
            }
        }

        private void CreateButtonConfigUI(string buttonName, string displayName)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            
            // 按钮标题
            var titleBlock = new TextBlock 
            { 
                Text = displayName, 
                FontWeight = FontWeights.Bold, 
                Margin = new Thickness(0, 0, 0, 5) 
            };
            panel.Children.Add(titleBlock);

            // 动作选择
            var actionPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            actionPanel.Children.Add(new TextBlock { Text = "动作类型:", Width = 80, VerticalAlignment = VerticalAlignment.Center });
            
            var actionComboBox = new ComboBox { Width = 200, Margin = new Thickness(5, 0, 0, 0) };
            PopulateActionComboBox(actionComboBox);
            
            // 设置当前值
            if (_config.ButtonConfigs.ContainsKey(buttonName))
            {
                actionComboBox.SelectedValue = _config.ButtonConfigs[buttonName].ActionType;
            }
            
            actionComboBox.SelectionChanged += (s, e) => OnActionTypeChanged(buttonName, actionComboBox);
            _actionComboBoxes[buttonName] = actionComboBox;
            actionPanel.Children.Add(actionComboBox);
            panel.Children.Add(actionPanel);

            // 自定义快捷键输入
            var shortcutPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            shortcutPanel.Children.Add(new TextBlock { Text = "快捷键:", Width = 80, VerticalAlignment = VerticalAlignment.Center });
            
            var shortcutTextBox = new TextBox 
            { 
                Width = 200, 
                Margin = new Thickness(5, 0, 0, 0),
                IsEnabled = false
            };
            
            // 设置当前值
            if (_config.ButtonConfigs.ContainsKey(buttonName))
            {
                shortcutTextBox.Text = _config.ButtonConfigs[buttonName].CustomShortcut ?? "";
                shortcutTextBox.IsEnabled = _config.ButtonConfigs[buttonName].ActionType == ButtonActionType.CustomShortcut;
            }
            
            _shortcutTextBoxes[buttonName] = shortcutTextBox;
            shortcutPanel.Children.Add(shortcutTextBox);
            shortcutPanel.Children.Add(new TextBlock 
            { 
                Text = " (格式: Ctrl+Alt+T)", 
                Foreground = System.Windows.Media.Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0)
            });
            panel.Children.Add(shortcutPanel);

            // 添加分隔线
            panel.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 0) });

            ButtonConfigPanel.Children.Add(panel);
        }

        private void PopulateActionComboBox(ComboBox comboBox)
        {
            var actionTypes = Enum.GetValues(typeof(ButtonActionType)).Cast<ButtonActionType>();
            
            foreach (var actionType in actionTypes)
            {
                var description = GetEnumDescription(actionType);
                comboBox.Items.Add(new ComboBoxItem 
                { 
                    Content = description, 
                    Tag = actionType 
                });
            }
            
            comboBox.DisplayMemberPath = "Content";
            comboBox.SelectedValuePath = "Tag";
        }

        private string GetEnumDescription(Enum value)
        {
            FieldInfo field = value.GetType().GetField(value.ToString());
            DescriptionAttribute attribute = field.GetCustomAttribute<DescriptionAttribute>();
            return attribute?.Description ?? value.ToString();
        }

        private void OnActionTypeChanged(string buttonName, ComboBox comboBox)
        {
            var selectedActionType = (ButtonActionType)comboBox.SelectedValue;
            var shortcutTextBox = _shortcutTextBoxes[buttonName];
            
            // 只有选择自定义快捷键时才启用文本框
            shortcutTextBox.IsEnabled = selectedActionType == ButtonActionType.CustomShortcut;
            
            if (selectedActionType != ButtonActionType.CustomShortcut)
            {
                shortcutTextBox.Text = "";
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // 验证并保存配置
            foreach (var kvp in _actionComboBoxes)
            {
                var buttonName = kvp.Key;
                var actionComboBox = kvp.Value;
                var shortcutTextBox = _shortcutTextBoxes[buttonName];

                if (!_config.ButtonConfigs.ContainsKey(buttonName))
                {
                    _config.ButtonConfigs[buttonName] = new ButtonConfiguration { ButtonName = buttonName };
                }

                var config = _config.ButtonConfigs[buttonName];
                config.ActionType = (ButtonActionType)actionComboBox.SelectedValue;
                config.DisplayName = GetEnumDescription(config.ActionType);
                config.CustomShortcut = shortcutTextBox.Text?.Trim() ?? "";

                // 验证自定义快捷键
                if (config.ActionType == ButtonActionType.CustomShortcut && string.IsNullOrEmpty(config.CustomShortcut))
                {
                    MessageBox.Show($"请为 {buttonName} 输入自定义快捷键", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}