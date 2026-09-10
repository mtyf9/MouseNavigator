using MouseNavigator.Contracts;
namespace MouseNavigator.App;
internal static class DefaultProfiles
{
    public static NavigatorConfiguration Configuration() => new(3, Create(), []);
    public static IReadOnlyList<MenuProfile> Create() =>
    [
        new(3, "global", "全局导航", [],
        [
            new("top", "windows.window.preview"), new("right", "windows.maximize"),
            new("bottom", "windows.minimizeAll"), new("left", "windows.tasks")
        ]),
        // First-run example; saved user menus take precedence on subsequent launches.
        new(3, "explorer", "文件资源管理器", [new("explorer.exe")],
        [
            new("top", "windows.maximize"), new("right", "windows.window.preview"),
            new("bottom", "windows.minimizeAll"), new("left", "windows.tasks")
        ], 10)
    ];
}
