# P2 · 应用骨架与外壳

> 阶段：P2 · 应用骨架与外壳（见 `PORTING_PLAN.md` §2）
> 日期：2026-11-08 ~ 2026-11-08
> 环境：Windows 10.0.26100 · .NET SDK 10.0 · WinAppSDK **2.6.5**（`WindowsPackageType=None`，非打包）· CommunityToolkit.Mvvm 8.4.2 · Serilog 4.3.0 + Serilog.Sinks.File 7.0.0
> 结论：**可继续（P3 可开工）** —— 启动/主窗口/菜单树/7 类对话框/选择器/进度窗/语言切换/设置往返/关闭流程全部实机跑通；
> 本阶段顺手修掉 6 个真实缺陷（其中 2 个会让 P3 直接踩坑）。遗留 1 个 🟡（输入框与 Skip/SkipAll 框在 P2 没有可达入口，只有代码级证据）与 1 个待决项（D8 提权，已给结论但需 P5 复验）。

---

## 2. 结论速览

| # | 验证项 / 交付项 | 结论 | 对计划的影响 |
|---|---|---|---|
| V1 | 启动链路（Serilog + 未观察任务异常 + CrashReporter + 自定义 `Main`） | ✅ 通过 | `DISABLE_XAML_GENERATED_MAIN` 方案保留 |
| V2 | DI 容器装配（R13 服务定位器顺序） | ✅ 通过 | R13 关闭；P3 可直接解析任何 VM |
| V3 | 单实例（`AppMutex` → `AppInstance` + 激活重定向） | ✅ 通过 | 第二实例退出码 0，主实例前台化 |
| V4 | 主窗口骨架 + 完整菜单树（File 5 / Tools 9 / Operations 11 / Help 2） | ✅ 通过 | 与 Alt 逐项对齐（仅少 `Crash?`，D6 既定） |
| V5 | 确认框 / 异常框 / 信息框 / 多按钮选择框 | ✅ 通过 | `ContentDialog` 方案成立；长文本需包滚动区（已修） |
| V6 | 输入框（`InputTextAsync`）/ Skip·SkipAll·Abort（`ShowOperationInterruptAsync`） | 🟡 有条件通过 | 实现完成但 **P2 无可达 UI 入口**（调用方在 P3/P4 窗口里）；P3 第一次用到时必须现场复验 |
| V7 | 文件夹选择器 | ✅ 通过 | 新 `Microsoft.Windows.Storage.Pickers` 非打包可用，无需 HWND |
| V8 | 文件选择器（`.addonroot` 打开框） | 🟡 有条件通过 | 同一 `PickerService` 分支；P2 只验证到文件夹选择器与导入流程，文件框未取得界面证据 |
| V9 | 进度窗（`OperationsProgressWindow` + 删除模板） | ✅ 通过 | 非模态、`IsDone` 前拒关；删除任务完成后显示“已完成。1 个项目已被删除。” |
| V10 | 中英文切换 | ✅ 通过 | 改 `Settings.json` 的 `Language` 重启即英文 UI（R7 行为确认） |
| V11 | 关闭流程（保存 → 关窗 → 异步 Dispose → 15s 兜底） | ✅ 通过 | 3 次关闭均 `All services disposed successfully.`，0.5s 内退出 |
| V12 | `%Documents%\FireAxe\Settings.json` 与 Alt 一致 | ✅ 通过 | 31 个键、1215 字节，**SHA256 前后完全一致** |
| V13 | 资源/资产随构建落盘（`Assets`、`ExportedAssets`） | ✅ 通过 | 修复前 VPK 冲突检查整段抛异常（见 3.12 坑 1） |

**总判断：** P2 目标（“启动 → 主窗口 → 各种对话框 → 设置持久化 → 正常退出”）全部达成。
P3 可以开工，但必须遵守 §5 的 8 条硬约束——尤其是第 2 条（窗口关闭后不得再触碰 `AppWindow`）与第 4 条（资源必须显式复制）。

---

## 3. 逐项详述

### 3.1 启动与生命周期

**✅ 通过。** 做法：csproj 加 `<DefineConstants>$(DefineConstants);DISABLE_XAML_GENERATED_MAIN</DefineConstants>`，
用 `Program.Main` 顶替 XAML 生成的入口，使 **日志与单实例检查都早于 XAML 初始化**（与 Alt 的 `Program.cs` 顺序一致）。

顺序：`CultureManager` → `SetupLogger`（Serilog File，按天滚动保留 7 份）→ `TaskScheduler.UnobservedTaskException` →
`RegisterObjectExplanations` → `SingleInstance.TryAcquire` → `InitializeComWrappers` → `Application.Start(...)`。

原始证据（`bin\x64\Debug\net10.0-windows10.0.26100.0\Logs\Log20261108.txt`）：

```
2026-11-08 01:33:22.605 +08:00 [INF] FireAxe Start (Version: 0.0.0)
```

**差异（已记入 §9）**：Alt 还挂了 `WriteTo.Console()`。WinExe 没有控制台，该 sink 写不出去，故只保留 File sink。

### 3.2 DI 装配（R13 关闭）

**✅ 通过。** `App.OnLaunched` 里先 `BuildServiceProvider()`，**再**赋 `AppServices.Provider`，**之后**才解析 `AppSettings` 与任何 VM——
满足 R13 要求的“Provider 必须早于 VM 构造”。注册表与 Alt 的 `ServiceCollection` 一一对应，只多了 6 个平台抽象的实现：

| 抽象（P1 定义） | 实现（P2） |
|---|---|
| `IUiDispatcher` | `WinUiDispatcher`（`DispatcherQueueTimer`） |
| `IAppThemeService` | `WinUiAppThemeService`（逐窗口 `ElementTheme`） |
| `IDialogService` | `DialogService`（`ContentDialog` + `SemaphoreSlim(1,1)`） |
| `IPickerService` | `PickerService`（`Microsoft.Windows.Storage.Pickers`） |
| `IClipboardService` | `WinUiClipboardService` |
| `IWindowService` / `IActiveWindowProvider` | `WindowService`（同时实现两者） |
| `IImageLoader` | `ImageLoader`（`BitmapImage` + `DecodePixelWidth`） |

证据：应用能通过 DI 解析出整棵对象图并打开真实目录（见 3.4），且 `AppServices` 静态入口未出现空服务症状。

### 3.3 单实例

**✅ 通过。** `AppInstance.FindOrRegisterForKey("cecf873b…")`（沿用 Alt 的 GUID）替代命名 Mutex；
落败实例在后台线程 `RedirectActivationToAsync` 后 `return 0`；主实例订阅 `Activated` 把主窗口拉到前台。

坑：`RedirectActivationToAsync` **不能在 UI 线程同步等待**（消息泵还没起来会自锁）。实现里用
`Semaphore(0,1)` + `Task.Run(...).Wait()` 的官方写法。

### 3.4 主窗口骨架与菜单树

**✅ 通过。** 实机打开真实目录后的菜单项（UI 树枚举，非目测）：

- **文件**：打开目录 / 关闭目录 / 导入 / 导入 .addonroot文件 / 设置（+2 条分隔线）
- **工具**：问题列表 / 标签管理器 / 下载列表 / VPK冲突列表 / 替换总览 / 创意工坊VPK查找器 / 文件清理器 / 模组名称自动设置器 / 文件名修复器
- **操作**：推送 / 启动游戏 / 检查 / 清除缓存 / 随机选择 / 随机选择(选中项) / 删除多余的VPK文件 / 删除多余的VPK文件(选中项) / 导出所选项目为addonroot文件 / 删除文件不存在的模组 / 删除文件不存在的模组(选中项)
- **帮助**：检查更新 / 关于FireAxe

与 `FireAxe_Alt\FireAxe.GUI\Views\MainWindow.axaml` L17-86 逐项比对：**唯一缺项是 `Crash? → Crash!!!`**，符合 D6。

状态栏三态实测：`检查`按钮 + `检查中...` + `⚠ 发现 7 个问题。` + `推送`按钮；空目录时显示空态（`打开目录` 按钮 + 提示文案）。

**ViewLocator 的落地方式**：`MenuBar` + `MenuFlyoutItem` 直接写死在 `MainWindow.xaml`（Avalonia 的 `Menu` 无等价物）；
`AddonNodeExplorerView` 位置在 P2 是**占位文本块**（P3 替换）：`Text="Placeholder — ported in P3."`。

### 3.5 对话框体系

**✅（4 类已实测）/ 🟡（2 类仅代码级）。** 统一 `ContentDialog`，所有请求过 `SemaphoreSlim(1,1)` 串行化
（WinUI 同一 `XamlRoot` 只允许一个 ContentDialog）。实测到的真实弹窗：

| API | 触发路径 | 证据 |
|---|---|---|
| `ConfirmAsync` | 启动时“正在打开高版本的文件…你确定要继续吗？”；删除模组前确认 | 截图 `P2-confirm.png`（标题“警告”，按钮“是/否”） |
| `ShowInfoAsync` | `文件 → 设置` 命中未移植占位 | 标题 `FireAxe`，正文 `AppSettingsWindow has not been ported yet (P4).`，按钮“确定” |
| `ShowChoiceAsync` | 启动自动检查更新命中 0.7.6 | 英文 UI 下 `Update / New version available: 0.7.6 / Go to download / Ignore` |
| `ShowExceptionAsync` | `LastOpenDirectory` 指向含损坏 `.addonroot` 的目录 | 标题“错误”，正文=说明+异常+完整调用栈 |

日志证据（占位路径）：

```
2026-11-08 01:40:40.403 +08:00 [WRN] WindowService.OpenSettingsWindow was called but AppSettingsWindow is not ported yet (P4).
```

**返回值语义的端到端证据**：更新框点 `Ignore` 后，`Settings.json` 的 `SuppressedUpdateRequestVersion` 由 `v0.7.4` 变为 `0.7.6`——
说明 `ShowChoiceAsync` 的按钮索引确实被正确映射回 VM 的分支（不是“弹出来就算通过”）。

**🟡 输入框 / Skip·SkipAll·Abort**：`DialogService.InputTextAsync`、`ShowOperationInterruptAsync` 均已实现，
但调用方是 `AddonTagEditorViewModel`（P3）、`AddonTagManagerViewModel`（P4）、`AddonNodeExplorerViewModel`（P3 的导入报错路径），
**P2 的 UI 上没有任何入口能触发它们**，因此只计代码级证据。P3 第一次真正用到时现场复验，不得默认通过。

### 3.6 选择器

**文件夹选择器 ✅。** `文件 → 打开目录` 弹出 `#32770` Win32 对话框，标题“打开目录”，按钮“选择文件夹 / 取消”，
目录层级 `此电脑 > <WORKSPACE_ROOT>`。取消（`WM_CLOSE`）后应用正常继续。

**文件选择器 🟡。** `PickerService.PickFileAsync` 与文件夹分支共用 `GetWindowId()` 与错误处理，代码路径一致，
但本轮验证被桌面控制权中断，**没有取得该框的界面证据**；P3 打开 `.addonroot` / 导入 vpk 时会高频使用，届时复验。

坑：WinAppSDK 2.x 的新 Picker **不需要 HWND**，但**必须要 `WindowId`**——传 `AppWindow.Id`，不要传 `AppWindow` 本身。

### 3.7 主题与资源

**✅ 通过（接受观感差异，D4/R3）。** 54 个 Avalonia `Selector=` 样式不做一一还原；`WinUiAppThemeService` 把
`AppColorTheme`（Default/Light/Dark）映射为逐窗口根元素 `ElementTheme`。18 个 `StreamGeometry` 图标在 P2 用
Segoe 字形过渡（E8B7 目录 / E73E 勾 / E7BA 警告 / E898 推送），**P3 统一换成 `Path` 或 Segoe Fluent Icons**。

### 3.8 保存调度

**✅ 通过。** `SaveManager` 由 Avalonia `DispatcherTimer(interval, priority, callback)` 改为
`IUiDispatcher.StartTimer(3s, () => { SaveAll(); return true; })`（内部 `DispatcherQueueTimer`）。
关闭时 `SaveAll(true)` 强制落盘，逐个 `ISaveable` 吞异常并记日志（与 Alt 相同）。

### 3.9 关闭流程（本阶段最大的坑）

**✅ 通过。** 链路：主窗口 `AppWindow.Closing` → `args.Cancel = true` + 置 `IsShuttingDown` →
`SaveAll(true)` → `WindowService.CloseAllWindows()` → `services.DisposeAsync()` → `Exit()`；
同时起 15 s 看门狗线程，超时 `Environment.Exit(-1)`。

**A 级坑：先关窗再 Dispose，导致对已销毁窗口写 `Title`。** 修复前日志：

```
2026-11-08 01:45:09.358 +08:00 [ERR] Exception occurred during disposing services.
System.Runtime.InteropServices.COMException (0x800710DD): 操作标识符不正确。
The WinUI Desktop Window object has already been closed.
   at Microsoft.UI.Xaml.Window.set_Title(String value)
   at FireAxe.Views.MainWindow.OnViewModelPropertyChanged(...)
   at FireAxe.ViewModels.MainWindowViewModel.Dispose()
```

原因：`MainWindowViewModel.Dispose()` 把 `AddonRoot` 置空 → 派生状态变化 → 对已关闭窗口设 `Title`。
解法：窗口 `Closed` 时置 `_isClosed` 并退订 `PropertyChanged`，事件处理器首行早退。
修复后连续 3 次关闭：

```
2026-11-08 01:53:54.483 +08:00 [INF] All services disposed successfully.
2026-11-08 02:04:32.547 +08:00 [INF] All services disposed successfully.
2026-11-08 02:07:00.590 +08:00 [INF] All services disposed successfully.
```

**B 级坑：进度窗的拒关逻辑会卡住关闭流程。** `OperationsProgressWindow` 在 `!IsDone` 时 `args.Cancel = true`
（照抄 Alt）。若关闭时它还在运行，`CloseAllWindows()` 永远关不掉它 → 只能等 15 s 看门狗强杀。
解法：`App.IsShuttingDown` 为真时不再取消关闭。

### 3.10 设置持久化

**✅ 通过。** 运行前 `Settings.json`（Alt 生成的真实文件，31 键 / 1215 字节）与“启动→开目录→关目录→改语言→退出”之后的文件：

```
before = C6088DE78FE4FBF43BE82EB2EC9310A1B376898794F40F8F30A718D52F608F0B
after  = C6088DE78FE4FBF43BE82EB2EC9310A1B376898794F40F8F30A718D52F608F0B
```

**逐字节相同**（键集合、键序、浮点格式、`null` 都一致）。测试用的原始文件副本留作证据：`docs/phase-notes/P2-settings-before.json`。

窗口几何：`MainWindowWidth/Height` 以 **DIP** 存档（`AppWindow.ResizeClient(size * RasterizationScale)` 换算），
`AppWindow.Changed` 回写，`_isApplyingGeometry` 防回环；最大化状态存 `IsMainWindowMaximized`。

### 3.11 语言切换（R7 行为确认）

**✅ 通过。** 把 `Settings.json` 的 `Language` 改为 `"en"` 重启后，菜单栏变为 `File / Tools / Operations / Help`，
对话框变为 `Update / New version available: 0.7.6 / Go to download / Ignore`。
支持集仍是 `{"en","zh-Hans"}`（`CultureManager.SupportedCultureStrings`）。
**热切换不重建已物化文本**——与 P0 的 R7 结论一致，切换需重开窗口/重启进程。

### 3.12 本阶段修掉的 6 个坑（都会在 P3 复发，务必记住）

1. **A｜`ExportedAssets` 没进输出目录 → VPK 冲突检查整段抛 `AggregateException`。** Alt 用 `<None Include="ExportedAssets\**" CopyToOutputDirectory="PreserveNewest"/>`；
   csproj 里必须显式声明（本项目用 `<None Update="ExportedAssets\**" …/>`）。修复前日志满是
   `DirectoryNotFoundException: …\ExportedAssets\CommonVpkAddonConflictIgnoringFiles\HalfLife2.asset`，修复后为 0 条。
2. **B｜`required` 成员让 XAML 编译器产出不可编译的 `XamlTypeInfo.g.cs`（CS9035）**，表面症状是被吞成 `WMC9999`。
   凡是被 `{x:Bind}` 路径摸到的自定义类型，属性一律 `init` + 默认值，不要 `required`。
3. **B｜`ContentDialog` 的字符串内容不换行、不滚动。** 异常框带调用栈时，对话框宽到超出窗口被裁掉。
   解法：正文统一包进 `ScrollViewer(MaxWidth 480, MaxHeight 320)` + `TextWrapping=Wrap`（文本可选中，属于有意改进）。
4. **B｜两个输出树。** 直接 `build` csproj 时 `Platform` 落到 AnyCPU → `bin\Debug`；`build` 解决方案 → `bin\x64\Debug`。
   一度“改了没生效”就是因为跑的是 `bin\Debug` 里的旧二进制。解法：csproj 固定 `<Platform>x64</Platform>`。
5. **B｜`DispatcherQueueTimer` 没有嵌套 `TickEventHandler`。** 必须用
   `Windows.Foundation.TypedEventHandler<DispatcherQueueTimer, object>`。
6. **C｜`AppWindow` 在 2.6.5 没有 `Closed` 事件**（只有 `Closing` / `Changed` / `Destroying`）。窗口生命周期用
   `Microsoft.UI.Xaml.Window.Closed`。

---

## 4. 风险联动

| 风险号 | 变化 | 理由 |
|---|---|---|
| R3（观感无法一一还原） | ↓ 下调 | Fluent 落地方案已定（隐式样式 + `ElementTheme` + Segoe 字形），观感差异在 P2 已接受并跑通 |
| R6（无自动化 UI 回归） | ↑ 保持确定 | P2 全部靠手工 + UI 树/日志取证；9 个单测仍绿，但覆盖不到 UI |
| R7（本地化热切换需重建绑定） | ✖ 转确定 | P2 实测：改 `Language` 重启即生效；热切换不重建（已写进 §9 差异） |
| R13（`AppServices.Provider` 初始化顺序） | ✖ 消失 | DI 已在 `App.OnLaunched` 落地，Provider 早于任何 VM 构造 |
| R4（非打包 Picker/剪贴板需 HWND） | ✖ 保持低 | 新 Picker 只需 `WindowId`；文件夹选择器实测可用 |
| **R16** | ✚ 新增 | **关闭顺序陷阱**：先 `CloseAllWindows()` 再 `DisposeAsync()`，VM 的 `Dispose` 会碰到已销毁窗口（0x800710DD）。已修，见 3.9 |
| **R17** | ✚ 新增 | **双输出树**导致“改了没生效”一类误判（已用 `<Platform>x64</Platform>` 消除） |
| **R18** | ✚ 新增 | **`ContentDialog` 文本布局不自动收敛**（长文本撑爆窗口），后续任何长消息对话框都要包滚动区 |

---

## 5. 对下一阶段的硬约束（照做清单）

1. **对话框一律走 `IDialogService`**；禁止在 View/VM 里 `new ContentDialog`。需要新形态（如带图标的框）先改服务接口，再改调用方。
2. **任何 Window 在 `Closed` 里必须退订 `ViewModel.PropertyChanged` 并置 `_isClosed`**，事件处理器首行早退。
   违反 → 关闭时抛 `COMException 0x800710DD`，且只在退出瞬间暴露。
3. **会跑后台任务的窗口必须让 `App.IsShuttingDown` 旁路自己的拒关逻辑**，否则关闭流程只能等 15 s 强杀。
4. **新增任何非常规目录（`Assets`、`ExportedAssets`、以后的 `Data`）都必须在 csproj 里显式 `CopyToOutputDirectory`**，
   并在运行时日志里确认无 `DirectoryNotFoundException`。
5. **只用 `bin\x64\Debug\net10.0-windows10.0.26100.0\FireAxe.exe`**（`Platform` 已固定 x64）。跑之前先 `dotnet build FireAxe.sln`。
6. **自定义类型不要用 `required` 成员**（会被 `{x:Bind}` 摸到 → `XamlTypeInfo` 生成失败 → 假 `WMC9999`）。
7. **`WindowService` 里的 12 个 `OpenNotPortedYet(...)` 占位必须逐个替换**（P4/P5），替换后删除占位分支与 `ShowNotPortedDialogAsync`。
8. **ViewLocator 尚未最终定型**：`MainWindow.xaml` 里 `AddonNodeExplorerView` 目前是占位文本块，P3 必须换成显式 `DataTemplate` 或注册表并在本文件回写结论。

> **P3 回写（2026-11-08）**：第 8 条**已结清** —— 不引入 ViewLocator。`MainWindow.xaml` 直接实例化
> `<local:AddonNodeExplorerView DataContext="{x:Bind ViewModel.AddonNodeExplorerViewModel, Mode=OneWay}" … />`，
> Section 视图由 `AddonNodeView.xaml.cs` 按 VM 类型 `new` 并设 `DataContext`（与 Alt 同做法）。
> 第 7 条的占位从 12 个变成 **15 个**（P3 因详情面板又加了标签编辑器 / 自定义图片 / 节点选择器三个占位）。
> 详见 `P3-main-view.md` §3.13。

---

## 6. 遗留项（分级）

| 项 | 级别 | 归属阶段 | 备注 |
|---|---|---|---|
| 输入框 / Skip·SkipAll·Abort 框只有代码级证据，无可达 UI 入口 | 🟨 可延后 | P3 | 第一次真实调用必须现场复验并补证据 |
| 文件选择器（`FileOpenPicker`）未取得界面证据 | 🟨 可延后 | P3 | 与文件夹选择器同分支；`.addonroot` 导入时即验证 |
| 12 个工具窗仍是占位对话框 | 🟨 可延后 | P4 / P5 | 占位实现集中在 `WindowService.OpenNotPortedYet` |
| 18 个 `StreamGeometry` 图标仍是 Segoe 字形过渡 | 🟨 可延后 | P3 | 与 Explorer 一起统一换 |
| `AppSettingsWindow`（设置窗）未移植 | 🟨 可延后 | P4 | 当前点“设置”弹占位框 |
| D8 提权策略：manifest 保持 `asInvoker`，符号链接依赖开发者模式 | ⬜ 观察 | P5 复验 | 见 §7 的 D8 结论；推送功能实测失败则回退“按需提权重启” |
| `ImportAsync` 的导入结果窗在 P2 是非模态（Alt 是 `ShowDialog`） | ⬜ 观察 | P4 | 行为差异小；若发现窗口叠放问题再改模态 |
| 未移植窗口的占位文案是英文硬编码（`"… has not been ported yet (P4)."`) | ⬜ 观察 | P4 | 全部替换后自然消失，不进 resx |

---

## 7. 附：实测证据

> 以下为 2026-11-08 本机运行样例，数值随机器与负载浮动。

**构建与测试**

```
dotnet build FireAxe.sln -c Debug      → 已成功生成。 0 个警告 0 个错误
dotnet test  FireAxe.Tests -c Debug    → 已通过! 失败 0，通过 9，已跳过 0，总计 9
```

**启动 / 关闭 / 占位**

```
2026-11-08 01:33:22.605 [INF] FireAxe Start (Version: 0.0.0)
2026-11-08 01:40:40.403 [WRN] WindowService.OpenSettingsWindow was called but AppSettingsWindow is not ported yet (P4).
2026-11-08 01:53:54.483 [INF] All services disposed successfully.
2026-11-08 02:04:32.547 [INF] All services disposed successfully.
2026-11-08 02:07:00.590 [INF] All services disposed successfully.
```

**设置往返（SHA256）**

```
before = C6088DE78FE4FBF43BE82EB2EC9310A1B376898794F40F8F30A718D52F608F0B
after  = C6088DE78FE4FBF43BE82EB2EC9310A1B376898794F40F8F30A718D52F608F0B
```

**对话框栈的可滚动性（UIA 取证）**

```
窗格 "无法加载.addonroot文件…" [v:0.0%]        ← ScrollViewer，滚动位置 0%，可滚动
按钮 "确定" (1311,1148)
```

**导入 → 生成 addon 节点（`_p2-scratch\.addonroot`）**

```json
{ "Nodes": [ { "$type": "FireAxe.LocalVpkAddonSave, FireAxe.Core", "Name": "p2-test-addon", ... } ], "CustomTags": [] }
```

**进度窗**：标题 `删除模组`，正文 `已完成。1 个项目已被删除。`，详情区可展开（非模态独立窗口 515×237）。

**D8 结论（本阶段拍板，标 🟡 待 P5 复验）**：保持 `app.manifest` 的 `asInvoker`，符号链接依赖 Windows 开发者模式；
P5 实现“推送”时若符号链接失败，则回退到“检测失败 → 提示并以 `runas` 重启”的按需提权方案。理由：Alt 的 `requireAdministrator`
会让每次启动都过 UAC，而当前开发机会话非管理员，无法在 P2 验证提权路径的真实行为；把结论绑到真正用到符号链接的 P5 更可控。

**测试用临时工件**：`.p2-scratch\`（空 addonroot → 损坏 addonroot → 含 vpk 的 addonroot 三态）已在验证后删除；
`%Documents%\FireAxe\Settings.json` 已还原为运行前内容（哈希一致）。
