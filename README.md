# MouseNavigator · WinUI 3

基于 C# / WinUI 3 的鼠标中键悬浮菜单，支持应用专属菜单、自定义快捷键和窗口切换。

## 在 Visual Studio 2026 中继续开发

1. 安装 **WinUI 应用程序开发**与 **.NET 桌面开发**工作负载、.NET 10 SDK、Windows SDK 10.0.26100。
2. 打开 **MouseNavigator.WinUI.slnx**，将 **MouseNavigator.App** 设置为启动项目。
3. 选择 **Debug / x64**，还原 NuGet 包后按 F5。使用普通用户权限即可。
4. 如果 VS 已打开旧方案配置，请重新加载方案；方案 x64 已显式映射到启动项目 x64。
5. 运行目标为 Windows 11（最低 build 22000）。当前交付 x64，ARM64 发布留待验证后加入。

使用 Windows App SDK **2.4.0 正式版中的组件**：WinUI **2.3.6**、Foundation **2.3.9**、InteractiveExperiences **2.1.6**，以及 Windows SDK BuildTools **10.0.26100.9169**。依赖版本通过 packages.lock.json 固定。应用采用非打包启动，Windows App SDK 随输出携带；本机需安装 .NET 10 Desktop Runtime。首次还原需要下载 WinUI 及其组件依赖。

```powershell
dotnet build MouseNavigator.WinUI.slnx -p:Platform=x64
dotnet run --project tests/MouseNavigator.Tests
dotnet run --project tests/MouseNavigator.WindowsSmoke
```

WindowsSmoke 会创建临时测试窗口、验证焦点和最小化恢复，然后关闭测试进程。它只切换自己创建的测试窗口。

## 当前操作

- 启动后默认启用中键手势；主界面开关可暂停。
- **轻点中键**：重放普通中键点击（鼠标按下到松开的原生拖动语义不保留）。
- **按住并移动超过 10 DIP**：在起点附近显示四向菜单。
- **松开中键**：执行指针当前所处方向的动作。
- **移回中心、移出圆环或按 Esc**：取消本次手势。
- 左/右：上一个/下一个窗口。连续切换保持顺序稳定；跨显示器包含同一虚拟桌面中的窗口。
- 上：任务视图；下：全部最小化。
- 文件资源管理器有单独菜单示例，上方改为“最大化窗口”。
- 主窗口可最小化；关闭主窗口会释放全局鼠标监听并退出。托盘、开机启动尚未迁移。

窗口列表第一次按系统窗口枚举顺序建立，此后保留仍存在窗口的顺序，新窗口追加，消失窗口移除。不跟随任务栏顺序，也不是窗口空间位置排序；切换后不会按最近使用情况重排。

## 项目结构

| 项目 | 职责 |
| --- | --- |
| MouseNavigator.Contracts | 动作插件协议、平台能力接口、应用上下文与菜单模型 |
| MouseNavigator.Core | 插件注册与执行、菜单匹配、配置草稿与持久化、稳定窗口循环、手势几何 |
| MouseNavigator.Windows | Win32 鼠标监听、窗口过滤/激活、虚拟桌面查询、键盘输入、DPI 与悬浮窗适配 |
| MouseNavigator.Actions.BuiltIn | 7 个 Windows 动作与配置驱动的自定义快捷键插件 |
| MouseNavigator.App | WinUI 3 主界面、图形化菜单编辑、悬浮菜单、手势协调与组合入口 |
| tests | 核心回归检查、Windows 集成检查、Debug 专用 WinUI 渲染检查 |

仓库只保留当前 WinUI 方案、源码、仍在使用的回归测试和示例配置。NuGet 依赖由 PackageReference 还原，不需要根目录 packages 文件夹。开发记录、设计稿和本地备份集中在 local-dev/，由 .gitignore 排除，不随代码提交。

## 自定义菜单与配置文件

打开“应用菜单”，点击四向预览中的方向，即可选择内置动作、不执行动作或自定义快捷键。快捷键支持直接录入，或通过 Ctrl / Alt / Shift / Win 复选框与主键列表组合。

- 新增应用菜单，填写进程名；未匹配的应用使用全局菜单。
- 点击“保存并应用”立即生效，启动时自动读取已保存配置。
- 配置位于 %LOCALAPPDATA%\MouseNavigator\settings.json；上一份有效配置保存为 settings.json.bak。
- 导入 JSON 先加载为草稿，保存后应用；导出 JSON 包含全部菜单与快捷键，可在其他电脑导入。
- 配置损坏时尝试备份恢复，读取错误会在界面提示。默认菜单仅用于首次运行及恢复回退。
- 关闭时会提示保存未完成的修改。

可直接导入 [示例菜单](examples/menus.example.json)，其中包含全局导航及 Code / Visual Studio 的 Ctrl+S 配置。

应用进程名不区分大小写，可省略 .exe，多个进程名用分号分隔，不填写完整路径。多个菜单同时匹配时，优先级数值高者生效，同优先级按菜单 ID 排序。全局菜单必须保留。

快捷键支持字母、数字、F1–F24、导航键与常用标点键。点击“录入快捷键”后输入组合；Esc 退出录入。系统保留组合也可通过复选框和主键列表配置。

### 配置格式

JSON 根对象包含 schemaVersion（当前为 1）、profiles 和 shortcuts：

| 对象 | 字段 |
| --- | --- |
| 菜单 | schemaVersion、id、name、applications、entries、priority |
| 应用匹配 | processName |
| 方向动作 | slot（Top / Right / Bottom / Left）、actionId |
| 快捷键 | id（以 shortcuts. 开头）、name、keys |

keys 使用 Windows 虚拟键码，修饰键在前、主键在最后；例如 Ctrl+S 是 [17, 83]。方向可留空，表示不执行动作。文件限制为 1 MB、最多 100 个菜单与 400 个快捷键；未知版本、非法结构或缺失快捷键定义会被拒绝。未知插件动作保留引用，并在界面标记不可用。

保存采用同目录临时文件和原子替换，保留上一份有效配置。主文件损坏时优先加载备份，否则暂用默认菜单；启动时不会覆盖损坏的文件。配置文件只包含数据和动作引用，不加载程序集或执行脚本。

### 验证

核心测试覆盖窗口循环、菜单匹配、动作路由、配置校验、草稿隔离和备份恢复。WindowsSmoke 使用独立临时窗口验证激活、最小化恢复、对话框定位及快捷键接收，不向其他应用发送测试按键。

Debug 构建还包含真实 WinUI 自检，覆盖中键手势、下拉框选择、配置即时切换和界面保存：

~~~powershell
& .\src\MouseNavigator.App\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\MouseNavigator.App.exe --smoke-test "$PWD\artifacts\desktop-smoke"
~~~

自检将截图、配置与结果写入指定目录，不修改个人配置；Release 不编译该入口。人工验收仍需覆盖物理中键操作、快捷键录入、文件选择器、多屏缩放和特殊权限应用。

## 后续扩展

界面动效、触发键更换、应用匹配条件扩展、共享平台、第三方插件安装/更新机制仍留待下一步。当前插件通过代码显式注册，尚无第三方 DLL 扫描或插件商店。
