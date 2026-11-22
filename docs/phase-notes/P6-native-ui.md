# P6 · 原生界面实施记录（尚未完成验收）

## 1. 元信息

- 阶段：P6；日期：2026-11-10～2026-11-11。
- 环境：Windows x64；.NET SDK 10.0.402；目标 net10.0-windows10.0.26100.0，MinVersion 17763；Windows App SDK 2.6.5。
- 新依赖：CommunityToolkit.WinUI.Controls.SettingsControls 8.2.251219，MIT；使用公开 SettingsCard/SettingsExpander。NuGet 资产最低 Windows 19041，实际旧系统兼容性尚未实测，不据构建成功推断支持 17763。
- 一句话结论：首轮导航、设置分类、浏览详情和工具窗代码已落地，构建和冒烟测试通过；P6 仍在实施，未达到视觉/交互验收标准。

### 2026-11-11 列表名称双击修复

- 用户反馈名称区域显示文本光标、双击不能进组。行视图与磁贴视图名称 TextBlock 原启用 IsTextSelectionEnabled，拦截双击选词；现关闭文本选择和名称的命中测试，让列表接收双击。详情编辑控件不变。
- 构建证据：p6-list-doubleclick-rebuild.log:5–9，0 警告/错误，17.72 秒（首次被运行中的 exe 锁定，关闭后重建通过）；文字区域双击、Ctrl/Shift 多选及 F2 重命名仍待手动点验。

### 2026-11-11 推送游戏路径初始化修复

- 用户反馈推送提示游戏路径为空；打开库时遗漏将 AppSettings.GamePath 赋给 AddonRoot.GamePath，只有后续修改设置才同步。现于 LoadFile 后、首次 Check 前初始化，保留已有设置变更同步。
- 回归覆盖首次打开、设置变更及关闭重开；比较设置规范化后的路径。首次测试因斜杠规范化断言不符失败，修正断言后 p6-gamepath-init-retest.log:7 为 10/10。p6-gamepath-init-build.log:5–9 为 0 警告/错误，17.03 秒。
- 同步更新无库时启动游戏命令可用的旧断言。实际推送会写入游戏目录，未代替用户执行，待手动复验。

### 2026-11-11 应用标识补全

- 中英文“关于”、崩溃提示、重复运行提示及崩溃报告标题统一使用 FireAxeWinUI3。
- 关于页项目链接与默认更新仓库改为 AkinaLTS/FireAxeWinUI3；上游 FireAxe 链接独立保留。目前仓库私有，现有匿名更新检查不能读取私有 Releases；未加入凭据或修改用户已保存的仓库设置。

### 2026-11-11 标题栏、显示名与启动游戏补充

- 主窗及关于页显示名改为 FireAxeWinUI3；保留既有数据目录与程序集名称。
- 系统标题栏通过 AppWindow.TitleBar.PreferredTheme 跟随应用深浅色设置，默认跟随系统；继续使用原生窗口按钮。
- 启动游戏命令取消必须打开插件库的限制：已有库使用其 GamePath，否则使用应用设置 GamePath；保留路径验证、启动参数读取及错误对话框。已确认本机配置路径下存在 left4dead2.exe；没有代替用户实际启动游戏，原反馈的运行时失败尚待复验。
- p6-title-game-build.log:5–9：构建 0 警告/错误，21.68 秒。p6-title-game-tests.log:4：既有 --no-build 冒烟测试 9/9，不覆盖实际游戏启动或标题栏观感。

## 2. 结论速览

| 验证项 | 等级 | 对计划的影响 |
|---|---|---|
| Debug 集成构建 | ✅ | p6-integrated-build-final.log:5–9，0 警告/错误 |
| 冒烟测试复测 | ✅ | p6-tests-retest.log:4，9/9；首次搜索超时仍列观察 |
| 27 项主窗命令映射 | 🟡 | 源码清单已产出，仍须实际点验可达性 |
| 分类设置与原生控件 | 🟡 | 七类页面落地；编辑、保存、依赖、窄窗需实测 |
| 浏览、多选及上下文 | 🟡 | 保留 VM/列表并增加布局门控；焦点与切页需点验 |
| 工具窗一致性与长列表 | 🟡 | 页头、留白、部分有限高度列表完成；仍有密集布局待打磨 |
| 主题/DPI/性能/截图/兼容系统 | 🟡 | 无实机证据，保持 P6 未完成 |

## 3. 逐项详述

实施覆盖与逐窗待验项见 ../P6-ui-map.md；设置逐项映射见 ../P6-settings-map.md。

2026-11-11 材质补齐：主窗通过公开 `Window.SystemBackdrop = new MicaBackdrop()` 接入 Mica，由 WinUI 管理主题、激活及系统降级。主窗根资源将宿主页面画布设为透明，卡片仍使用系统卡片画刷；窄窗详情覆盖层保留 `SolidBackgroundFillColorBaseBrush`，避免列表文字透出。构建证据为仓库根目录 `p6-mica-build.log`；壁纸色调、深浅色、关闭透明效果及高对比度效果仍待实机点验，不据构建成功宣称视觉验收通过。

主窗改用 NavigationView，插件库、工具、下载及帮助保持已创建视图实例，通过 Visibility 切换；隐藏库时解除窗口快捷键回退。下载继续打开原共享队列窗口，避免同一生命周期 VM 被两个视图同时激活。浏览器保留原 ListView 与选择同步，920 DIP 以下将详情折叠为可关闭覆盖区，排序/视图收进飞出菜单。

新增中文资源已从 zh-CN 调整为 zh-Hans，与 CultureManager.SupportedCultureStrings 及既有 Texts.zh-Hans 一致；ResourceManager 的父文化回退不会从 zh-Hans 横向寻找 zh-CN，原命名会导致简体中文选项下的新文案回退英文。

设置采用 Toolkit SettingsCard/SettingsExpander 与七分类导航，保留原数值提交事件和设置对象。七项字符串编辑改为原生 TextBox/PasswordBox TwoWay，四项数值保留 EditableTextBlock 提交契约。WinUI BindingExpression 无 UpdateTarget，异常路径通过重新应用 ParentBinding 恢复绑定；非法代理地址即时反馈及保存往返仍待实机验证。刷新忽略文件按钮改为调用现有异步方法，修复不存在的旧 Command 绑定。需要用真实设置文件副本检查保存往返，尤其字符串及数值编辑。

修复下载窗口构造函数缺少 InitializeComponent；移除其外层 ScrollViewer，ListView 拥有有限高度。问题、冲突、文件名修复和工坊查找的外层数据宿主改为 ListView，嵌套展开内容仍有 ItemsControl；尚未量测大库性能。

构建命令：`dotnet build FireAxe.sln -c Debug --no-restore -v minimal`。最终集成日志第 5–9 行成功。中间 XAML 编译 WMC9999 的实际原因是绑定不存在的 Texts.Close，已改为现有 Cancel；不能把该错误误判为 SDK 损坏。中间一次构建读取到编辑中的 AboutWindow，XML 闭合后重建成功；最终不再有该错误。设置写入期间的一次 CS1697 校验和警告在集成重建后消失。

测试命令：`dotnet test FireAxe.Tests/FireAxe.Tests.csproj -c Debug --no-build --no-restore -v minimal`。首次 8/9，SearchText_FiltersCurrentNodes 超时：`the search never narrowed the list to Alpha`（p6-tests.log:3–11）。单项复测 1/1（p6-search-retest.log:4），全套复测 9/9（p6-tests-retest.log:4）。未为此修改 VM/Core；偶发异步时序仍列观察项 B。

Computer Use 关闭，Snapshot 返回工具不可用；未捕获运行截图、未验证实际窗口加载和交互。源码良构及构建不能证明 Toolkit 运行资源、焦点、主题或低版本 Windows 行为。问题 A：P6 验收证据缺失；问题 C：主窗底部和密集工具命令在窄窗仍需检查。

用户实测（2026-11-11 17:21）：主窗点击设置导致进程崩溃。Logs/Log20261111.txt:32–44 明确记录 `Failed to create a 'Microsoft.UI.Xaml.Controls.IconElement' from the text 'Game'. [Line: 47 Position: 129]`，发生在 AppSettingsWindow.InitializeComponent。已将非法 Icon="Game" 改为 WinUI Symbol Play，并同步设置映射；设置加载修复待用户复测，不能据重新构建宣称已通过。此问题说明 XAML 编译成功和主窗启动存活不覆盖延迟创建的设置窗口。

用户再次实测发现 Icon="Warning" 同样无效。已以 Windows App SDK 2.6.5 metadata/Microsoft.UI.Xaml.xml 中 Symbol 枚举成员逐项核对 Views 全部 Icon/Symbol 字面量，发现剩余唯一无效项 Warning，改为 Important；设置七个导航图标全部改为显式 SymbolIcon，避免 IconElement 字符串转换延迟到运行期。p6-symbol-audit-build.log:5–9 构建 0 警告/错误；重启 PID 13940 主窗存活，设置加载仍待用户复测。上次只修首个报错遗漏后续图标，不能视为完整验证。

单窗口要求（2026-11-11 用户实测反馈）：此前独立设置/工具窗口方案被明确否决，改为主窗内页面导航。全部 19 个次级视图改为 HostedPage（UserControl），WindowService 不再创建次级原生窗口；主窗提供返回栈、常规页缓存与临时页完成事件。切换内容使旧次级页 Unloaded，保留实例状态；插件目录变化和关闭主窗清理缓存。最终导航守卫及关闭清理已补齐：进度不可关闭时拒绝切页、恢复导航选择；已有缓存保持存活，新临时页被拒绝时完成等待；主窗关闭不再渲染已关闭 UI。p6-single-window-final-guard-build.log:5–9 构建 0 警告/错误（18.57 秒）；p6-single-window-tests.log:4 冒烟 9/9。已启动 PID 3972，5 秒仍存活；单窗口页面交互待用户实测，不声明验收通过。

## 4. 风险联动

| 风险 | 变化 | 理由 |
|---|---|---|
| R24 导航/焦点/生命周期 | ↑上调 | 已改导航与详情布局，静态门控需实机验证 |
| Toolkit 旧系统兼容 | ✚新增 | 依赖资产最低 19041 与当前 MinVersion 17763 不一致，需实际兼容测试或明确调整支持范围 |
| 搜索异步超时 | 保持观察 | 首次失败，独立及整套复测通过 |
| 数据密集布局性能 | 保持 | 部分外层虚拟化完成，嵌套及总览未测 |

## 5. 对下一阶段的硬约束

- 继续完成 P6 实机验收后再关闭阶段；不要把界面验收全部移交 P7。
- 开启可用桌面控制后捕获主窗、设置及代表工具窗真实截图，记录主题、尺寸与 DPI。
- 点验所有 27 个命令入口、七类设置保存往返、切页/关闭重开/多选/焦点/快捷键。
- 测试浅色、深色、高对比度、100/150/200% 缩放及窄窗；修复截断和不可达操作。
- 对比代表大库加载/滚动/搜索/内存，并验证最低支持系统的 Toolkit 资源。
- 保持 Core 业务、文件格式、共享下载服务与 VM 生命周期语义。
- 阶段正式收尾时将代码、阶段记录、计划与索引放入同一个提交。

## 6. 遗留项（分级）

- 🟥 P6：真实运行、截图、主题/DPI/窄窗/键盘/设置持久化与逐窗操作证据。
- 🟥 P6：Toolkit 在支持系统上的资源加载和兼容结论。
- 🟨 P6：密集工具命令、总览列宽和主窗底部窄窗布局打磨；大库性能对比。
- ⬜ P6/P7：搜索测试偶发超时；保留首次失败及两次复测证据。
- 🟨 P7：原 P3～P5 遗留与最终全量回归保持原归属。

## 7. 附：实测证据

日志位于仓库根目录，均为本次机器运行样例，耗时随环境变化：

- p6-culture-verified-build.log：中文文化名修正后构建成功；编译产物 zh-Hans/FireAxe.resources.dll 包含 FireAxe.Resources.NativeUiTexts.zh-Hans.resources 与既有 Texts.zh-Hans.resources。首轮复制被本轮启动探针 PID 8452 锁定，已正常关闭该探针后重建；未关闭既有 PID 26328。
- p6-settings-verified-build.log:5–9：字符串回写及 WinUI API 修正后的最终构建成功，0 警告、0 错误，22.00 秒。
- p6-integrated-build-final.log:5–9：此前集成成功，0 警告，0 错误，22.05 秒。
- p6-tests-retest.log:4：0 失败、9 通过、0 跳过。
- p6-tests.log:3–11：首次搜索超时。
- p6-search-retest.log:4：单项复测通过。
- ../P6-ui-map.md：主命令及工具窗覆盖与待验项。
- ../P6-settings-map.md：设置逐项去向与兼容契约。

启动补测（2026-11-11）：用 Start-Process 启动最终 Debug exe，WaitForExit(5000) 返回 False；进程 PID 8452，HasExited=False，MainWindowTitle=FireAxe。这是 5 秒存活与窗口标题证据，不能替代页面渲染和逐窗交互验证。既有 PID 26328 未关闭。

再次调用桌面 Snapshot 仍返回 `Computer Use is switched off`，实机操作证据继续待补。尚无真实改版前后截图；不使用示意图或构建日志代替。
