using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace MouseNavigator_WPF
{
    /// <summary>
    /// 按钮动作类型枚举
    /// </summary>
    public enum ButtonActionType
    {
        [Description("无动作")]
        None = 0,
        [Description("切换到下一个桌面")]
        NextDesktop = 1,
        [Description("切换到上一个桌面")]
        PreviousDesktop = 2,
        [Description("最小化所有窗口")]
        MinimizeAll = 3,
        [Description("最大化当前窗口")]
        MaximizeWindow = 4,
        [Description("任务视图")]
        TaskView = 5,
        [Description("切换到下一个窗口")]
        NextWindow = 6,
        [Description("切换到上一个窗口")]
        PreviousWindow = 7,
        [Description("自定义快捷键")]
        CustomShortcut = 8
    }

    /// <summary>
    /// 单个按钮配置
    /// </summary>
    public class ButtonConfiguration
    {
        public string ButtonName { get; set; }
        public ButtonActionType ActionType { get; set; }
        public string DisplayName { get; set; }
        public string CustomShortcut { get; set; } // 格式: "Ctrl+Alt+T"
        
        public ButtonConfiguration()
        {
            ActionType = ButtonActionType.None;
            DisplayName = "未配置";
            CustomShortcut = "";
        }
    }

    /// <summary>
    /// 悬浮球配置
    /// </summary>
    public class FloatingBallConfig
    {
        public Dictionary<string, ButtonConfiguration> ButtonConfigs { get; set; }
        
        public FloatingBallConfig()
        {
            ButtonConfigs = new Dictionary<string, ButtonConfiguration>();
            InitializeDefaultConfig();
        }
        
        private void InitializeDefaultConfig()
        {
            // 初始化默认配置
            ButtonConfigs["Button0"] = new ButtonConfiguration 
            { 
                ButtonName = "Button0", 
                ActionType = ButtonActionType.None, 
                DisplayName = "中心按钮" 
            };
            
            ButtonConfigs["Button3"] = new ButtonConfiguration 
            { 
                ButtonName = "Button3", 
                ActionType = ButtonActionType.NextDesktop, 
                DisplayName = "下一个桌面" 
            };
            
            ButtonConfigs["Button6"] = new ButtonConfiguration 
            { 
                ButtonName = "Button6", 
                ActionType = ButtonActionType.MinimizeAll, 
                DisplayName = "最小化所有" 
            };
            
            ButtonConfigs["Button9"] = new ButtonConfiguration 
            { 
                ButtonName = "Button9", 
                ActionType = ButtonActionType.PreviousDesktop, 
                DisplayName = "上一个桌面" 
            };
            
            ButtonConfigs["Button12"] = new ButtonConfiguration 
            { 
                ButtonName = "Button12", 
                ActionType = ButtonActionType.TaskView, 
                DisplayName = "任务视图" 
            };
        }
    }
}