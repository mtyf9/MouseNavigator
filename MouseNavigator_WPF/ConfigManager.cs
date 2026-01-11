using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace MouseNavigator_WPF
{
    /// <summary>
    /// 配置文件管理器
    /// </summary>
    public static class ConfigManager
    {
        private static readonly string ConfigFileName = "FloatingBallConfig.json";
        private static readonly string ConfigFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MouseNavigator",
            ConfigFileName
        );

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        public static void SaveConfig(FloatingBallConfig config)
        {
            try
            {
                // 确保目录存在
                string directory = Path.GetDirectoryName(ConfigFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 序列化配置
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string jsonString = JsonSerializer.Serialize(config, options);
                File.WriteAllText(ConfigFilePath, jsonString, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 从文件加载配置
        /// </summary>
        public static FloatingBallConfig LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string jsonString = File.ReadAllText(ConfigFilePath, System.Text.Encoding.UTF8);
                    var config = JsonSerializer.Deserialize<FloatingBallConfig>(jsonString);
                    return config ?? new FloatingBallConfig();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载配置失败，将使用默认配置: {ex.Message}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // 如果文件不存在或加载失败，返回默认配置
            return new FloatingBallConfig();
        }

        /// <summary>
        /// 获取配置文件路径
        /// </summary>
        public static string GetConfigFilePath()
        {
            return ConfigFilePath;
        }
    }
}