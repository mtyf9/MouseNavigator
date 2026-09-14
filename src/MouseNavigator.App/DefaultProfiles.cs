using MouseNavigator.Contracts;
namespace MouseNavigator.App;
internal static class DefaultProfiles
{
    public static IReadOnlyList<MenuProfile> Templates() =>
    [
        new(3,"template-browser","浏览器",[new("msedge.exe"),new("chrome.exe"),new("firefox.exe")],
        [
            new("refresh","windows.browser.refresh"),new("new-tab","windows.browser.newTab"),
            new("next-tab","windows.browser.nextTab"),new("reopen-tab","windows.browser.reopenTab"),
            new("close-tab","windows.browser.closeTab"),new("previous-tab","windows.browser.previousTab"),
            new("back","windows.browser.back"),new("forward","windows.browser.forward")
        ],10,IsGlobalDefault:false),
        new(3,"template-text","文本编辑",[new("notepad.exe")],
        [
            new("copy","windows.editing.copy"),new("paste","windows.editing.paste"),
            new("cut","windows.editing.cut"),new("select-all","windows.editing.selectAll"),
            new("save","windows.editing.save"),new("find","windows.editing.find"),
            new("undo","windows.editing.undo"),new("redo","windows.editing.redo")
        ],10,IsGlobalDefault:false)
    ];
    public static NavigatorConfiguration InitializeMenus(NavigatorConfiguration configuration)
    {
        if(configuration.BuiltInMenusInitialized)return configuration;
        var menus=configuration.Profiles.ToList();
        foreach(var template in Templates())
            if(!menus.Any(p=>p.Id==template.Id||p.Name.Equals(template.Name,StringComparison.OrdinalIgnoreCase)))
                menus.Add(template);
        return configuration with{Profiles=menus.ToArray(),BuiltInMenusInitialized=true};
    }
    public static NavigatorConfiguration Configuration() => new(3, Create(), [],BuiltInMenusInitialized:true);
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
        ], 10),
        ..Templates()
    ];
}
