using Microsoft.Win32;
namespace MouseNavigator.Windows;

/// <summary>Unpackaged desktop app startup for the current Windows user.</summary>
public sealed class StartupRegistration(string executable, string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Run")
{
    private const string ValueName = "MouseNavigator";
    public string Command => BuildCommand(executable);
    public bool Enabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            return string.Equals(key?.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase);
        }
    }
    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            using var key = Registry.CurrentUser.CreateSubKey(keyPath);
            key.SetValue(ValueName, Command, RegistryValueKind.String);
        }
        else
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
    public static string BuildCommand(string executable)
    {
        if (!Path.IsPathFullyQualified(executable) || executable.Contains('"'))
            throw new ArgumentException("启动程序路径无效。");
        var command = $"\"{executable}\" --startup";
        if (command.Length > 260) throw new ArgumentException("程序路径过长，请将程序移到较短路径后启用开机启动。");
        return command;
    }
}