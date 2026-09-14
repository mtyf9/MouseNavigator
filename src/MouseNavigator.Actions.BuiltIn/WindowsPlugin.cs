using MouseNavigator.Contracts;
namespace MouseNavigator.Actions.BuiltIn;
public sealed class WindowsPlugin : INavigatorPlugin
{
    public string Id => "windows";
    public int ApiVersion => 1;
    public IReadOnlyList<INavigatorAction> CreateActions(IPlatformActions platform)
    {
        var actions=new List<INavigatorAction>();
        void Shortcut(string id,string name,string category,string glyph,params ushort[] keys)=>
            actions.Add(new Action(new(id,name,ActionGlyph(id,glyph),name,Category:category),c=>platform.SendShortcut(c.WindowHandle,keys)));
        void System(string id,string name,params ushort[] keys)=>Shortcut("windows."+id,name,"系统功能","\uE713",keys);
        void Window(string id,string name,string glyph,params ushort[] keys)=>Shortcut("windows."+id,name,"窗口与桌面",glyph,keys);
        void Browser(string id,string name,params ushort[] keys)=>Shortcut("windows.browser."+id,name,"浏览器","\uE774",keys);
        void Edit(string id,string name,params ushort[] keys)=>Shortcut("windows.editing."+id,name,"编辑操作","\uE70F",keys);
        void Media(string id,string name,params ushort[] keys)=>Shortcut("windows.media."+id,name,"播放器","\uE768",keys);
        actions.Add(new Action(new("windows.window.preview","窗口预览","\uE7C4","保持按住中键，选择缩略图后松开切换",ActionInteraction.WindowPreview,"窗口与桌面"),
            _=>ActionResult.Failure("请保持按住中键，移入窗口预览区域后选择窗口。")));
        Window("tasks","任务视图","\uE7C4",0x5B,0x09);
        Window("minimizeAll","全部最小化","\uE921",0x5B,0x4D);
        actions.Add(new Action(new("windows.maximize","最大化/还原窗口","\uE922","普通窗口最大化，已最大化时还原。",Category:"窗口与桌面"),c=>platform.ToggleMaximize(c.WindowHandle)));
        Window("desktop.previous","上一个桌面","\uE892",0x11,0x5B,0x25);
        Window("desktop.next","下一个桌面","\uE893",0x11,0x5B,0x27);
        Window("desktop.show","显示/返回桌面","\uE7F4",0x5B,0x44);
        Window("desktop.new","新建虚拟桌面","\uE710",0x11,0x5B,0x44);
        Window("snap.left","窗口靠左","\uE76B",0x5B,0x25);
        Window("snap.right","窗口靠右","\uE76C",0x5B,0x27);
        Window("restore","还原/最小化窗口","\uE923",0x5B,0x28);
        Window("close","关闭当前窗口","\uE711",0x12,0x73);
        System("explorer","文件资源管理器",0x5B,0x45);
        System("settings","Windows 设置",0x5B,0x49);
        System("search","Windows 搜索",0x5B,0x53);
        System("run","运行",0x5B,0x52);
        System("quickSettings","快捷设置",0x5B,0x41);
        System("notifications","通知中心",0x5B,0x4E);
        System("clipboard","剪贴板历史",0x5B,0x56);
        System("screenshot","截图工具",0x5B,0x10,0x53);
        System("taskManager","任务管理器",0x11,0x10,0x1B);
        System("projection","投影模式",0x5B,0x50);
        Edit("copy","复制",0x11,0x43);Edit("paste","粘贴",0x11,0x56);
        Edit("cut","剪切",0x11,0x58);Edit("undo","撤销",0x11,0x5A);
        Edit("redo","重做",0x11,0x59);Edit("selectAll","全选",0x11,0x41);
        Edit("save","保存",0x11,0x53);Edit("find","查找",0x11,0x46);
        Browser("back","后退",0x12,0x25);Browser("forward","前进",0x12,0x27);
        Browser("refresh","刷新",0x74);Browser("newTab","新建标签页",0x11,0x54);
        Browser("closeTab","关闭标签页",0x11,0x57);Browser("reopenTab","恢复关闭的标签页",0x11,0x10,0x54);
        Browser("nextTab","下一个标签页",0x11,0x09);Browser("previousTab","上一个标签页",0x11,0x10,0x09);
        Browser("address","地址栏",0x11,0x4C);Browser("bookmark","添加收藏",0x11,0x44);
        Browser("downloads","下载列表",0x11,0x4A);Browser("fullscreen","全屏",0x7A);
        Media("playPause","播放/暂停",0xB3);Media("next","下一曲",0xB0);
        Media("previous","上一曲",0xB1);Media("stop","停止播放",0xB2);
        Media("volumeUp","增大音量",0xAF);Media("volumeDown","减小音量",0xAE);Media("mute","静音/取消静音",0xAD);
        actions.Add(new Action(new("windows.applications.launch","启动自定义应用","\uE8A7","启动所选 .exe 程序，可配置启动参数",Category:"应用程序"),
            c=>c.Launch is null?ActionResult.Failure("请先在按钮配置中选择应用程序。"):platform.LaunchApplication(c.Launch)));
        actions.Add(new MacroAction(platform));return actions;
    }
    private static string ActionGlyph(string id,string fallback)=>id switch
    {
        "windows.explorer"=>"\uE8B7","windows.settings"=>"\uE713","windows.search"=>"\uE721",
        "windows.run"=>"\uE756","windows.quickSettings"=>"\uE9E9","windows.notifications"=>"\uE7F4",
        "windows.clipboard"=>"\uE77F","windows.screenshot"=>"\uE722","windows.taskManager"=>"\uE9D9",
        "windows.projection"=>"\uE7F4",
        "windows.editing.copy"=>"\uE8C8","windows.editing.paste"=>"\uE77F","windows.editing.cut"=>"\uE8C6",
        "windows.editing.undo"=>"\uE7A7","windows.editing.redo"=>"\uE7A6","windows.editing.selectAll"=>"\uE8B3",
        "windows.editing.save"=>"\uE74E","windows.editing.find"=>"\uE721",
        "windows.browser.back"=>"\uE72B","windows.browser.forward"=>"\uE72A","windows.browser.refresh"=>"\uE72C",
        "windows.browser.newTab"=>"\uE710","windows.browser.closeTab"=>"\uE711","windows.browser.reopenTab"=>"\uE7A7",
        "windows.browser.nextTab"=>"\uE76C","windows.browser.previousTab"=>"\uE76B","windows.browser.address"=>"\uE71B",
        "windows.browser.bookmark"=>"\uE734","windows.browser.downloads"=>"\uE896","windows.browser.fullscreen"=>"\uE740",
        "windows.media.playPause"=>"\uE768","windows.media.next"=>"\uE893","windows.media.previous"=>"\uE892",
        "windows.media.stop"=>"\uE71A","windows.media.volumeUp"=>"\uE995","windows.media.volumeDown"=>"\uE993",
        "windows.media.mute"=>"\uE74F",_=>fallback
    };
    private sealed class Action(ActionDescriptor descriptor,Func<ApplicationContext,ActionResult> execute):INavigatorAction
    {
        public ActionDescriptor Descriptor=>descriptor;
        public ValueTask<ActionResult> ExecuteAsync(ApplicationContext context,CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(execute(context));
        }
    }
}