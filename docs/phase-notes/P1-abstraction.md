# P1 · 平台抽象层 + ViewModel 重写

## 1. 元信息

| 项 | 值 |
|---|---|
| 阶段 | **P1**（`PORTING_PLAN.md` §3 P1） |
| 日期区间 | 2026-11-08（单日完成；含 3 个并行子任务的 VM 搬运） |
| 环境 | Windows 11 · .NET SDK 10.0 · `Microsoft.WindowsAppSDK` 2.6.5 · `Microsoft.Windows.SDK.BuildTools` 10.0.26100.9457 · `CommunityToolkit.Mvvm` **8.4.2** · `LangVersion=latest` · 非打包（`WindowsPackageType=None`） |
| 交付 | `FireAxe.WinUI3\Services\`（6 个抽象 + 4 个辅助）、`ViewModels\` 41 文件 7,345 行、`FireAxe.Tests\`（9 个冒烟测试）、占位壳窗口 |
| 一句话结论 | **VM 层零 ReactiveUI/Avalonia 依赖，`dotnet build` 0 警告 0 错误，9 个冒烟测试全绿，exe 可运行并渲染出 `x:Bind` 到静态资源类的文本** —— 6 条硬约束交给 P2。 |

## 2. 结论速览

| 验证项 | 结论 | 对计划的影响 |
|---|---|---|
| 44 个源 VM 的搬运（不含 4 个 `*Design.cs`） | ✅ 通过 | 41 个文件落地，取舍见 §6 |
| 57 个 `Interaction<T,U>` 归并为服务方法 | ✅ 通过 | 归并成 **3 个服务 25 个方法**（计划估 ~15），见 §3.2 |
| VM 层无 `ReactiveUI` / `Avalonia` 代码符号 | ✅ 通过 | §3.4 的 grep 两种写法均 0 命中 |
| `dotnet build` 通过 | ✅ 通过 | 0 警告 0 错误（§7.1） |
| 两个重窗的冒烟单测（3~5 个） | ✅ 通过 | 实际 9 个（§7.2），并顺带覆盖 `x:Bind`/命令禁用态语义 |
| exe 可运行 | ✅ 通过 | 窗口标题 `FireAxe`，正文渲染出 `Texts.AppDescription`（§7.3） |
| `IViewModelLifecycle` 取代 `IActivatableViewModel` | 🟡 有条件通过 | **语义变化点**：谁来调 `Activate()` 由 P2 定死（§5 第 1 条） |
| `AsyncRelayCommand` 的并发语义 | 🟡 有条件通过 | 默认禁止重入，与 Rx `CreateFromTask` 不同（§3.6） |
| XAML 编译期错误可诊断性 | 🟡 有条件通过 | XamlCompiler 会把真实错误吞成 `WMC9999`（§3.7），必须人工核对绑定成员 |

## 3. 逐项详述

### 3.1 翻译机制（怎么换的）

逐文件对照 `FireAxe_Alt\FireAxe.GUI\ViewModels\` 重写，映射按计划 §3 P1.1 执行，实际补充 3 条：

| 原机制 | 现机制 | 补充说明 |
|---|---|---|
| `IActivatableViewModel` + `WhenActivated` | `IViewModelLifecycle { Activate(); Deactivate(); }`（幂等） | 被回收复用的 `ListView` 容器需要重复激活/去激活，所以两个方法都必须可重入 |
| 每个 addon 一个 `CompositeDisposable` | `AddonSubscriptions`（属性名→处理器映射 + 解绑列表） | 只订阅"该 VM 真正显示的属性"，避免整棵树订阅 |
| `ObservableAsPropertyHelper` | 手写属性 + `SetProperty` + 显式重建 | 派生集合一律在 `CollectionChanged` 上重建 |
| Rx `Throttle(...)` | `Services\DebounceAction`（`Task.Delay` + 取消，有 dispatcher 时回 UI 线程） | 搜索 0.5 s、排序/标签 0.3 s、自定义图 0.5 s |
| `MessageBus.Current` | `WeakReferenceMessenger` + `AddonNodeJumpMessage` | 全工程仅 2 处（`AddonRefView` 发送、主窗接收） |
| `Avalonia.Threading.Dispatcher` | `IUiDispatcher.Post/InvokeAsync/StartTimer` | 定时器（剪贴板 0.5 s、自动重下 5 s、备份 1 min）改为 `StartTimer` |
| `Avalonia Bitmap` | `IImageSource`（`IImageLoader.DecodeToWidthAsync`） | `Image` 属性类型从 `Bitmap?` 变为 `IImageSource?`，P3 的 `ImageLoader` 实现负责包成 `WriteableBitmap` |
| `Avalonia Clipboard` | `IClipboardService.TryGetTextAsync/SetTextAsync` | — |

**证据**：`ViewModels\` 41 个文件顶部均有 `// Ported from FireAxe_Alt\...` 来源标注（计划 §6.4 要求）。

### 3.2 Interaction 归并的实际形状

计划估"3 个服务约 15 个方法"，实测 **25 个**：

| 服务 | 方法数 | 内容 |
|---|---:|---|
| `IDialogService` | 7 | `ConfirmAsync` / `ShowErrorAsync` / `ShowExceptionAsync` / `ShowInfoAsync` / `InputTextAsync` / `ShowOperationInterruptAsync`（→`OperationInterruptReply`）/ `ShowChoiceAsync`（更新提示的"前往下载 / 忽略"，返回索引） |
| `IPickerService` | 3 | `PickFolderAsync` / `PickFileAsync` / `SaveFileAsync`（选项类带 `Title`/`FilePatterns`/`DefaultFileExtension`） |
| `IWindowService` | 15 | 11 个 `Open*Window()` + `OpenNewWorkshopCollectionWindow` + `ShowImportResultAsync` + `ShowDeletionProgress` + `ActivateMainWindow` |

多出来的原因是 `IAppWindowManager` 的 11 个开窗方法必须进 `IWindowService`（计划 §3 P1.0 已写明"与既有 `IAppWindowManager` 合并"），而 `ShowChoiceAsync` 是源工程"有新版本"提示框的三按钮语义，`Confirm` 表达不了。

**坑（B 级）**：`ShowOperationInterruptAsync` 与 `ConfirmAsync` 不能合并——前者的 `SkipAll` 是"批量操作里跳过全部"，`bool` 承载不了。

### 3.3 对话框文案的搬家

源工程把文案写在 `Views\*Window.axaml.cs` 的 `RegisterHandler` 里。本次全部搬进 VM（VM 拥有本地化字符串），View 只剩"弹"这一个动作。

顺带抽出两个去重件：

- `ViewModels\RedundantVpkFileDialogs`：`DeleteRedundantVpkFiles` 在 `WorkshopVpkAddonViewModel` 与 `MainWindowViewModel` 各出现一次，文案/顺序完全一致。
- `Services\MessageComposer.CombineMessageAndException`：`CommonMessageBoxes` 的私有拼接逻辑，`ShowExceptionAsync` 的实现会用到。

**证据**：`ViewModels\MainWindowViewModel.cs` 内 `Texts.` 引用 40+ 处；`Views\` 目录下当前只有占位 `MainWindow.xaml(.cs)`，没有任何文案。

### 3.4 "零 ReactiveUI/Avalonia"的自查口径

两种 grep 都执行，结果不同，必须都看：

```powershell
# ① 代码符号：0 命中
Select-String -Path *.cs -Recurse -Pattern '^\s*using\s+(ReactiveUI|Avalonia|System\.Reactive|DynamicData)'
Select-String -Path *.cs -Recurse -Pattern ': ReactiveObject|IActivatableViewModel|ObservableAsPropertyHelper|ReactiveCommand<|Interaction<|WhenActivated\(|DisposeWith\(|ViewModelActivator|DispatcherTimer\.Run|MessageBus\.Current'

# ② 注释里仍会出现这些词（解释"原来是啥"），命中 60+ 行
```

**结论**：① 全 0；② 的命中全部在 `//` 注释或 XML 文档里，属溯源信息。若验收方要求"文本级 0 命中"，删注释即可，不影响编译。

唯一保留的 `NotifyAndSetIfChanged(...)` 调用在 `AppSettings.cs`——那是 `FireAxe.Core` 自带的 `FireAxe.ObservableObject` 方法（R11 的产物），不是 Rx 的 API。

### 3.5 `AppSettings` 的改造面

`AppSettings` 是 P1 里唯一"不是纯 VM 但要动刀"的文件（839 行 → 仍在 `FireAxe.WinUI3\` 根目录）：

| 原 | 现 |
|---|---|
| `Application.Current!.RequestedThemeVariant = ...`（Avalonia） | 注入 `IAppThemeService`，`ColorTheme` setter 转调，`ColorThemeChanged` → `NotifyChanged` |
| `WhenAnyValue(...6 个属性).Throttle(300ms)` → 同步下载服务 | `ScheduleDownloadSettingsSync()`：`CancellationTokenSource` + `Task.Delay(300)`，回捕获的 `SynchronizationContext` |
| `ReactiveCommand LoadCustomVpkAddonConflictIgnoringFilesCommand` | `Task<IReadOnlySet<string>?> LoadCustomVpkAddonConflictIgnoringFilesAsync()` |
| `CultureManager` 里 `SemiTheme.OverrideLocaleResources` | 删除（Fluent 无对应物） |

### 3.6 已知语义差异：`AsyncRelayCommand` 不允许重入

Rx 的 `ReactiveCommand.CreateFromTask` **默认允许**并发重入；CommunityToolkit 的 `AsyncRelayCommand` **默认禁止**（执行中 `CanExecute` 为 false 且会忽略再次触发）。

本次未改动源语义，一律用默认值。受影响的是"用户可以连点两次"的命令（推送、导入、开目录）。已在 `MainWindowViewModel` 里用手写 `IsOpeningDirectory` 补回"开目录期间不重复打开"的原逻辑。

**这条必须在 §5 交给 P3/P4 复核**：若某个窗口的原行为依赖"并发两次下载/两次检查"，需显式传 `AsyncRelayCommandOptions.AllowConcurrentExecutions`。

### 3.7 XamlCompiler 吞错误的坑（A 级，但有解）

C# 侧编译干净后，XAML Pass2 报：

```
Xaml Internal Error error WMC9999: 未能找到任何适合于指定的区域性或非特定区域性的资源。
请确保在编译时已将"Microsoft.UI.Xaml.Markup.Compiler.ErrorMessages.resources"正确嵌入…
```

设 `VSLANG=1033` / `DOTNET_CLI_UI_LANGUAGE=en` **都无效**——它加载的是 XamlCompiler 自己的附属程序集，不是 MSBuild 的本地化。

真实原因是占位 XAML 里写了 `{x:Bind r:Texts.AppName, Mode=OneTime}`，而 `Texts` **没有 `AppName` 这个资源键**（源的窗口标题是字面量 `Title="{Binding TitleExtraInfo, StringFormat=FireAxe{0}}"`）。XamlCompiler 抛异常时又格式化不了异常消息，于是只留下 WMC9999。

**处置**：改绑真实存在的 `Texts.AppDescription` + 标题改字面量 `"FireAxe"`，Pass2 立刻通过。

**可作为通用判断规则**：见到 `WMC9999` + "ErrorMessages.resources" 同时出现，直接怀疑"某个 `{x:Bind}` 的成员名不存在"，逐个核对绑定成员，不要怀疑 SDK 安装。

### 3.8 编译期才暴露的 4 类错误（都已在仓库内修掉）

| 错误 | 症状 | 根因 | 修法 |
|---|---|---|---|
| `CS0260` | 4 个类必须 `partial` | 用了 `[RelayCommand]`/`[ObservableProperty]`，源生成器要加分部声明 | 类声明加 `partial` |
| `CS0103` | `ThrowIfInvalid()` / `RegisterInvalidHandler(...)` "名称不存在" | 扩展方法**不能**像实例方法那样裸调用（不是 CS1061！） | 写成 `this.ThrowIfInvalid()` |
| `CS0122` | `ReadOnlyObservableCollection<T>.CollectionChanged` "不可访问" | 该事件是 `INotifyCollectionChanged` 的**显式**实现 | `((INotifyCollectionChanged)x).CollectionChanged += ...`（`PropertyChanged` 同理） |
| `CS0023` + `CS1503` | `!x is { } y` 报"运算符 `!` 无法应用"；`new AsyncRelayCommand<bool>(m, can)` 报"方法组无法转换为 AsyncRelayCommandOptions" | `!` 优先级高于 `is`；`AsyncRelayCommand<T>` 有 `(Func<T,Task>, AsyncRelayCommandOptions)` 重载抢投 | 写 `is not { } y`；`canExecute` 写成 `_ => CanExecute()` |

### 3.9 测试工程的可行性（计划里标为"待探"）

结论：**xunit 可以直接引用 WinUI 应用工程**，不需要 WinUI 运行时初始化，前提是测试只碰 VM 类型。

| 要求 | 值 |
|---|---|
| TFM | `net10.0-windows10.0.26100.0`（与被测工程一致） |
| 平台 / RID | `Platforms=x64` + `RuntimeIdentifier=win-x64` |
| `UseWinUI` | **不设**（设了会触发测试工程的 XAML 编译，无 XAML 时只会添麻烦） |
| 包版本 | `Microsoft.NET.Test.Sdk` 17.14.1 · `xunit` 2.9.3 · `xunit.runner.visualstudio` 3.1.4（`dotnet new xunit` 在该 SDK 下的默认组合） |

首次运行的 3 个失败全部是**我在测试里写错的假设**，不是移植缺陷，已在测试注释里写明正确语义：

1. 资源管理器初始 `CurrentGroup == null`（根层），`AddonRoot` 不是 `AddonGroup`，故新建的组 `Parent` 是 `AddonRoot`；
2. 导航栏是 `CurrentGroup` 的 `Group` 祖先链（与源实现一致），顶层组只有 1 项、根层为 0 项；
3. `DeleteCommand` 是 `IAsyncRelayCommand<bool>`，`CanExecute` 必须传参（`CanExecute(true)`），传 `null` 恒 false。

另有一次假失败：搜索测试在"计数短暂变成 1"时就读集合，被随后的一次延迟刷新覆盖成空。改为轮询**内容**（`Count == 1 && [0].Name == "Alpha"`）后稳定通过。

## 4. 风险联动

| 风险号 | 变化 | 理由 |
|---|---|---|
| R1（VM 重写行为漂移） | **↓ 下调**（高 → 中） | 41 个文件全部落地且编译零警告；两个重窗有 9 个测试钉住主链路（开/关目录、版本校验前置确认、排序/视图双向同步、新建/导航/移动节点、搜索与清除）；剩余漂移面集中在"View 怎么驱动 VM"（P2/P3） |
| R6（无自动化测试） | **↓ 下调**（确定 → 部分缓解） | 已建立 `FireAxe.Tests` 并覆盖批 4；其他 VM 尚无测试，仍按"手工回归"计 |
| R8（`CanExecute` 漏通知） | **↓ 下调** | 派生命令的禁用态改为 `NotifyAddonRootCommandsCanExecuteChanged()` / `NotifySelectionCommandsCanExecuteChanged()` 两个集中点 + 属性上的 `[NotifyCanExecuteChangedFor]`，测试已断言"无目录时推送/关闭/启动游戏为灰" |
| R9（生命周期语义变化） | 保持中，但**具体化** | 不再是"待定方案"，已固定为 `IViewModelLifecycle`；风险转移到 P2 是否每处都调 |
| R11（`ObservableObject` 撞名） | ✖ 消失（已闭环） | `ViewModelBase` 用 `global::CommunityToolkit.Mvvm.ComponentModel.ObservableObject` 吸收，全工程无一处直接继承 |
| R12（版本/语言版本组合） | ✖ 消失 | 8.4.2 + `latest` 实测通过 |
| **R13（新）** 服务定位器 `AppServices` 的初始化顺序 | ✚ 新增（中） | 嵌套 VM 用 `new` 构造（§6.3 禁止改签名），只能从静态 `AppServices` 取服务；**若 `AppServices.Provider` 在 VM 构造后才赋值，那些 VM 会静默拿到 `null` 并跳过功能**（如不启动定时器、不弹对话框） |
| **R14（新）** XamlCompiler 错误被吞成 `WMC9999` | ✚ 新增（中） | 见 §3.7；P2~P5 每次新增 XAML 都可能遇到，误判成本 ≥ 半天 |
| **R15（新）** `AsyncRelayCommand` 默认禁止重入 | ✚ 新增（低-中） | 见 §3.6；需要"并发两次"的地方必须显式开 `AllowConcurrentExecutions` |

## 5. 对下一阶段的硬约束

1. **`Activate()` / `Deactivate()` 必须由 View 显式调用**：窗口 `Loaded` → `Activate()`，`Closed` → `Deactivate()`；`ListView` 的 `ItemTemplate` 里，容器复用（`ContentTemplateRoot` 重新绑定数据）也必须重新调用目标 VM 的 `Activate()`。VM 不会自己启动。
2. **先注册服务、后构造 VM**：`App` 构造函数里先 `AppServices.Provider = ...`，再从 DI 解析 `MainWindowViewModel`。顺序反了不会报错，只会静默失效（R13）。
3. **服务实现必须非阻塞**：`IDialogService` / `IPickerService` 的每个方法返回 `Task`，实现里用 `ContentDialog` 的 `ShowAsync` 并 `await`；禁止 `.Result` / `.Wait()`。
4. **`IWindowService.ShowDeletionProgress` 立刻返回**，不要 `await` 传入的删除任务（源的语义是"开着进度窗，删除继续跑"）。
5. **命令绑定名**：`[RelayCommand]` 生成 `XxxCommand`；源 XAML 里"方法当命令用"的那些（`Pause` / `Resume` / `Cancel` / `RemoveJobViewModel` / `Remove`）在 WinUI 必须绑 `PauseCommand` / `ResumeCommand` / …。
6. **三个名字没加 `Command` 后缀**，且是有意为之：`ExportSelectedItemsAsAddonRootFile`、`OpenCustomVpkAddonConflictIgnoringFilesDirectory`（这两个源 XAML 就是这么绑的）、`DeleteCommand`（`IAsyncRelayCommand<bool>`，`CanExecute` 要传参）。
7. **XAML 里只准用 `{x:Bind r:Texts.Xxx, Mode=OneTime}`**，且**每个成员名先确认在 `Texts.Designer.cs` 里存在**（R14；`AppName` 就是这么翻车的）。
8. **`x:DataType` / `ItemTemplate` 的数据类不得用 `init` 属性**（P0 已有，重申）。
9. **预览图类型是 `IImageSource`**，不是 `Bitmap`：`ImageLoader` 实现要负责 `WriteableBitmap` 包装与 `Dispose`；VM 会在替换时 dispose 旧值。
10. **不要往 View 里再写对话框文案**；文案归 VM，`RedundantVpkFileDialogs` / `MessageComposer` 是唯一拼接入口。
11. **`TileViewSize` 目前是常量 200（占位）**：Semi 的 `size_addon_tile{,_large,_small}` 断言不变量在 P3 定死前不要当真值用。
12. **迁移期不改 `FireAxe_Alt`**（R10）。

## 6. 遗留项（分级）

### 🟥 阻断（P2 开工前必须解决）

| # | 条目 | 归属 |
|---|---|---|
| 1 | 6 个抽象全部**没有实现**：`IUiDispatcher` / `IClipboardService` / `IImageLoader` / `IDialogService` / `IPickerService` / `IWindowService`。当前 `App.xaml.cs` 是占位版，直接 `new Views.MainWindow()`，**一个 VM 都没实例化**。 | P2 |
| 2 | DI 容器与 `AppServices.Provider` 尚未装配（含 `HttpClient` 的代理/UA 配置、`DownloadService` 的 `ApplicationSettings`、`IAppThemeService`） | P2 |

### 🟨 可延后

| # | 条目 | 归属 |
|---|---|---|
| 1 | `AppSettingsViewModel` 的 4 个数字项是字符串代理，非法输入抛 `ArgumentException(Texts.ValueMustBeInteger)`；WinUI `TextBox` 需要接住并显示为校验错误（Avalonia 用 `DataValidationErrors`） | P4 |
| 2 | `MainWindowViewModel.InitIfNot` / `CheckUpdate` / `CheckClipboard` 仍是 `async void`（与源一致），异常会走 `App.UnhandledException` | P2 |
| 3 | `AddonRoot.DisposeAsync().AsTask()` 未 `await`（源里就有 `// TODO: await`），切目录时旧 AddonRoot 的释放是"发射后不管" | P3 |
| 4 | `AddonNameAutoSetJobViewModel.ApplyCommand` 是手写 `IRelayCommand`（`[RelayCommand]` 不能从非 `void` 同步方法生成，实测 `MVVMTK0007`） | P4 |
| 5 | `TargetAddonViewModel.Remove()` / `DependencyViewModel.Remove()` 没有对应命令（与 `AddonNodeDependenciesViewModel` 现状对齐）；View 层要么绑方法事件，要么补命令 | P4 |
| 6 | 重建式集合（`AddonNodeDependenciesViewModel`、`VpkAddonConflictingDetailsViewModel`、`FileCleanerViewModel`、`AddonNameAutoSetterViewModel` 的 `TargetAddonViewModel`）每次增删都**重建 VM 实例**，DynamicData 原本保留身份；若出现"选中项丢失/滚动位置跳动"就是它 | P4 |
| 7 | `FileCleanerViewModel` 的"添加来源"（`AddonNodePicker`）原本就在 `FileCleanerWindow.axaml.cs` 里、不是 Interaction，`IWindowService` 没有对应方法；现由 View 负责，VM 侧未新增成员 | P5 |

### ⬜ 观察

| # | 条目 | 归属 |
|---|---|---|
| 1 | `DebounceAction` 在没有 `IUiDispatcher` 时**内联执行**（测试里就是这条路）；真实应用必须有 dispatcher，否则回调落在 `Task.Delay` 的线程池线程上 | P2 |
| 2 | `AddonNodeSearchOptions.IncludeName` 默认 `true`，`Tags` 为 `null` 时不做标签过滤——搜索语义与源一致，依赖 Core 未改 | — |
| 3 | VM 层现在是 41 文件 / 7,345 行（源 44 文件 / 6,057 行，含 4 个 `*Design.cs`）。行数上涨主要来自 `#region` 化的显式命令属性与注释 | — |
| 4 | 剪贴板/自动重下/备份三个定时器在测试环境下不存在（`AppServices` 为空），因此**其行为未被测试覆盖**，仅有编译期保证 | P3 |

## 7. 附：实测证据

> 以下为某次运行样例，数值随环境浮动。

### 7.1 编译

```text
$ dotnet build FireAxe.sln -c Debug
  FireAxe.Core -> …\FireAxe.Core\bin\Debug\net10.0\FireAxe.Core.dll
  FireAxe.WinUI3 -> …\FireAxe.WinUI3\bin\x64\Debug\net10.0-windows10.0.26100.0\FireAxe.dll
已成功生成。
    0 个警告
    0 个错误
已用时间 00:00:13.04
```

### 7.2 冒烟测试

```text
$ dotnet test FireAxe.Tests\FireAxe.Tests.csproj -c Debug
已通过! - 失败:     0，通过:     9，已跳过:     0，总计:     9，持续时间: 1 s
```

9 个用例：

| 类 | 用例 | 钉住的语义 |
|---|---|---|
| `MainWindowViewModelTests` | `StartsEmpty_AndExposesNoAddonRoot` | 无目录时 `IsEmptyDirectory`、`TitleExtraInfo`、推送/关闭/启动游戏命令为灰，设置/下载列表命令可用 |
| | `OpenDirectory_CreatesAddonRootAndExplorer_CloseResets` | 开目录 → `AddonRoot` + 资源管理器（同实例）+ 派生属性连带通知；关目录 → 全部复位 |
| | `HigherVersionFile_AsksConfirm_AndIsRespected` | 目录版本文件高于当前版本时先确认；拒绝 → 不打开；同意 → 打开（这是 `Interaction` → `IDialogService` 的替代验证） |
| | `AppSettingsSortAndViewSettings_RoundTripThroughExplorer` | 设置 ↔ 资源管理器 排序/视图双向同步（源里靠 `WhenAnyValue` 双向链） |
| `AddonNodeExplorerViewModelTests` | `NewGroup_AddsNodeToCurrentGroup_AndSelectsIt` | 新建组进当前层、自动选中、删除命令可用 |
| | `Navigation_TracksCurrentGroupAndNavBar` | 进入组 / 返回上层 / 导航栏祖先链 / 切组清空选择 |
| | `MovingNodes_CanBeAddedAndCleared` | 移动节点的加入与清空 |
| | `ToggleOrderDirection_FlipsAscendingFlag` | 正/倒序切换可逆 |
| | `SearchText_FiltersCurrentNodes` | 搜索 0.5 s 去抖后只剩 `Alpha`，`ClearSearchText` 后恢复 2 项 |

### 7.3 exe 运行

```text
$ .\FireAxe.WinUI3\bin\x64\Debug\net10.0-windows10.0.26100.0\FireAxe.exe
RUNNING pid=31224
MainWindowTitle='FireAxe'
still alive after 10s: True
```

截图（`docs/phase-notes/assets/P1-shell-run.png`）中可见：

- 标题栏 `FireAxe`
- 正文 `FireAxe`、`FireAxe 0.0.0 · WinUI 3 · MainWindow`
- 卡片内 `一个管理求生之路2模组的工具。` ← **`{x:Bind r:Texts.AppDescription, Mode=OneTime}` 实际渲染结果**
- 卡片内两行状态文本

即：`x:Bind` → 静态资源类 → 本地化资源 的整条链路在运行时通。

### 7.4 零依赖自查

```text
=== using ReactiveUI/Avalonia/System.Reactive/DynamicData ===
(无输出)
=== forbidden symbols ===
（命中 60+ 行，全部出现在 // 注释与 /// <summary> 中，无一行是代码）
=== end ===
```
