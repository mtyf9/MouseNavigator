using System;
using System.Collections.Generic;
using System.Linq;
using WindowsInput.Native;

namespace MouseNavigator_WPF
{
    /// <summary>
    /// 快捷键解析器
    /// </summary>
    public static class ShortcutParser
    {
        private static readonly Dictionary<string, VirtualKeyCode> KeyMappings = new Dictionary<string, VirtualKeyCode>(StringComparer.OrdinalIgnoreCase)
        {
            // 修饰键
            { "Ctrl", VirtualKeyCode.CONTROL },
            { "Control", VirtualKeyCode.CONTROL },
            { "Alt", VirtualKeyCode.MENU },
            { "Shift", VirtualKeyCode.SHIFT },
            { "Win", VirtualKeyCode.LWIN },
            { "Windows", VirtualKeyCode.LWIN },
            
            // 字母键
            { "A", VirtualKeyCode.VK_A }, { "B", VirtualKeyCode.VK_B }, { "C", VirtualKeyCode.VK_C },
            { "D", VirtualKeyCode.VK_D }, { "E", VirtualKeyCode.VK_E }, { "F", VirtualKeyCode.VK_F },
            { "G", VirtualKeyCode.VK_G }, { "H", VirtualKeyCode.VK_H }, { "I", VirtualKeyCode.VK_I },
            { "J", VirtualKeyCode.VK_J }, { "K", VirtualKeyCode.VK_K }, { "L", VirtualKeyCode.VK_L },
            { "M", VirtualKeyCode.VK_M }, { "N", VirtualKeyCode.VK_N }, { "O", VirtualKeyCode.VK_O },
            { "P", VirtualKeyCode.VK_P }, { "Q", VirtualKeyCode.VK_Q }, { "R", VirtualKeyCode.VK_R },
            { "S", VirtualKeyCode.VK_S }, { "T", VirtualKeyCode.VK_T }, { "U", VirtualKeyCode.VK_U },
            { "V", VirtualKeyCode.VK_V }, { "W", VirtualKeyCode.VK_W }, { "X", VirtualKeyCode.VK_X },
            { "Y", VirtualKeyCode.VK_Y }, { "Z", VirtualKeyCode.VK_Z },
            
            // 数字键
            { "0", VirtualKeyCode.VK_0 }, { "1", VirtualKeyCode.VK_1 }, { "2", VirtualKeyCode.VK_2 },
            { "3", VirtualKeyCode.VK_3 }, { "4", VirtualKeyCode.VK_4 }, { "5", VirtualKeyCode.VK_5 },
            { "6", VirtualKeyCode.VK_6 }, { "7", VirtualKeyCode.VK_7 }, { "8", VirtualKeyCode.VK_8 },
            { "9", VirtualKeyCode.VK_9 },
            
            // 功能键
            { "F1", VirtualKeyCode.F1 }, { "F2", VirtualKeyCode.F2 }, { "F3", VirtualKeyCode.F3 },
            { "F4", VirtualKeyCode.F4 }, { "F5", VirtualKeyCode.F5 }, { "F6", VirtualKeyCode.F6 },
            { "F7", VirtualKeyCode.F7 }, { "F8", VirtualKeyCode.F8 }, { "F9", VirtualKeyCode.F9 },
            { "F10", VirtualKeyCode.F10 }, { "F11", VirtualKeyCode.F11 }, { "F12", VirtualKeyCode.F12 },
            
            // 方向键
            { "Up", VirtualKeyCode.UP }, { "Down", VirtualKeyCode.DOWN },
            { "Left", VirtualKeyCode.LEFT }, { "Right", VirtualKeyCode.RIGHT },
            
            // 其他常用键
            { "Space", VirtualKeyCode.SPACE }, { "Enter", VirtualKeyCode.RETURN },
            { "Tab", VirtualKeyCode.TAB }, { "Esc", VirtualKeyCode.ESCAPE },
            { "Delete", VirtualKeyCode.DELETE }, { "Backspace", VirtualKeyCode.BACK },
            { "Home", VirtualKeyCode.HOME }, { "End", VirtualKeyCode.END },
            { "PageUp", VirtualKeyCode.PRIOR }, { "PageDown", VirtualKeyCode.NEXT }
        };

        /// <summary>
        /// 解析快捷键字符串，返回虚拟键码数组
        /// </summary>
        /// <param name="shortcut">快捷键字符串，格式如 "Ctrl+Alt+T"</param>
        /// <returns>虚拟键码数组</returns>
        public static VirtualKeyCode[] ParseShortcut(string shortcut)
        {
            if (string.IsNullOrWhiteSpace(shortcut))
                return new VirtualKeyCode[0];

            var keys = shortcut.Split('+')
                              .Select(k => k.Trim())
                              .Where(k => !string.IsNullOrEmpty(k))
                              .ToArray();

            var result = new List<VirtualKeyCode>();

            foreach (var key in keys)
            {
                if (KeyMappings.TryGetValue(key, out VirtualKeyCode keyCode))
                {
                    result.Add(keyCode);
                }
                else
                {
                    throw new ArgumentException($"未识别的按键: {key}");
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// 验证快捷键字符串是否有效
        /// </summary>
        /// <param name="shortcut">快捷键字符串</param>
        /// <returns>是否有效</returns>
        public static bool IsValidShortcut(string shortcut)
        {
            try
            {
                var keys = ParseShortcut(shortcut);
                return keys.Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}