using MouseNavigator.Contracts;
namespace MouseNavigator.App;
internal static class DefaultProfiles
{
    public static NavigatorConfiguration Configuration() => new(1, Create(), []);
    public static IReadOnlyList<MenuProfile> Create() =>
    [
        new(1, "global", "全局导航", [],
        [
            new(RingSlot.Top, "windows.window.preview"), new(RingSlot.Right, "windows.maximize"),
            new(RingSlot.Bottom, "windows.minimizeAll"), new(RingSlot.Left, "windows.tasks")
        ]),
        // First-run example; saved user menus take precedence on subsequent launches.
        new(1, "explorer", "文件资源管理器", [new("explorer.exe")],
        [
            new(RingSlot.Top, "windows.maximize"), new(RingSlot.Right, "windows.maximize"),
            new(RingSlot.Bottom, "windows.minimizeAll"), new(RingSlot.Left, "windows.tasks")
        ], 10)
    ];
}
