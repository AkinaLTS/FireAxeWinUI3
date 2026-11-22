# FireAxe WinUI 3 前端重写 · 阶段化规划

> 源工程：`FireAxe_Alt`（Avalonia 11.3.9 / ReactiveUI 22.x / .NET 10，v0.7.6，git `main`）— **临时基线，完成后废弃**
> 目标工程：`FireAxeWinUI3`（WinUI 3 / Windows App SDK 2.6.5 / CommunityToolkit.Mvvm）— **全新项目 · 全新仓库 · 版本号 0.0.0 起手**
> 文档状态：**v12**（2026-11-11：P6 已开始实施；首轮代码与构建测试落地，实机视觉/交互验收未完成，见阶段记录）

## 变更记录

| 版本 | 变更 |
|---|---|
| v1 | 初稿。MVVM 框架倾向保留 ReactiveUI |
| **v2** | **D3 拍板：弃用 ReactiveUI，改用 `CommunityToolkit.Mvvm`。** ViewModel 层由"零改动复用"变为"整体重写"，工时与风险已重算 |
| **v3** | D1/D2/D5/D7 拍板：Core **拷贝进新工程独立维护**、非打包、`ListView` 选择模型、彻底不用 `System.Reactive`。**`FireAxe_Alt` 自此刻起冻结为只读参考** |
| **v4** | 明确 `FireAxeWinUI3` 是一个**全新项目 / 全新仓库 / 全新 git 历史**，不是 Alt 的分支；`FireAxe_Alt` 在 WinUI3 版完成后**将被废弃**。D4 定为 Fluent，D6 定为删除 `Crash?` 菜单。新增 §9「与 Alt 的有意差异」 |
| **v5** | **P0 完成**（见 `phase-notes/P0-spike.md`）。按实测修订：S2 用 `{x:Bind}` 替代 `x:Static`（退路作废）、MVVM 必须用 partial property + 8.4.2、Picker 改用新 API（R4 消失）、R2/R8 下调、新增 R11（`ObservableObject` 撞名） |
| **v6** | **P1 完成**（见 `phase-notes/P1-abstraction.md`）。按实测修订：抽象层实际 25 个方法（非 ~15）、新增 `IAppThemeService`、新增 `FireAxe.Tests`（xunit 可直接引用应用工程）、新增 R13/R14/R15（服务定位器顺序 / XamlCompiler 吞错误 / AsyncRelayCommand 重入）、§9 差异表补到 12 条 |
| **v7** | **P2 完成**（见 `phase-notes/P2-shell.md`）。D8 拍板 `asInvoker`（P5 复验）。新增 R16（关闭顺序陷阱）/R17（双输出树）/R18（ContentDialog 长文本不收敛）；R13、R4 关闭，R3 下调，R7 转确定；§9 差异表补到 18 条 |
| **v8** | **P3 完成**（见 `phase-notes/P3-main-view.md`）。按实测修订：D5 补注"`SelectionMode=Extended` + 代码同步，`SelectedItems` 不可绑"；新增 R19（`PathIcon.Data` 不可绑、`Geometry` 不可共享）/R20（派生 VM `new` 遮蔽属性 → 绑定静默失效）/R21（Core 后台线程通知 → `COMException 0x8001010E`）/R22（WinUI 夺焦点，快捷键需窗口级 fallback）/R23（集合型派生属性须用活集合）；R5 下调，R2 维持不关闭；§9 差异表补到 27 条 |
| **v9** | **P4 有条件完成**（见 `phase-notes/P4-tool-windows.md`）。首批工具窗路由已落地，Debug 全量构建 0 警告/错误且 exe 启动存活；交互点验与 AddAddonTag 自动补全列为遗留。 |
| **v10** | **P5 有条件完成**（见 `phase-notes/P5-data-dense-windows.md`）。三个重窗路由、冲突详情与解释逻辑核对完成；Debug 构建、9 项测试、exe 启动通过；独立工具窗交互点验列为收尾遗留（现归 P7）。 |
| **v11** | **新增 P6「Windows 原生界面重设计」**，以 Windows 11 设置的导航、页面层级与设置卡片为设计方向；原 P6「收尾」整体顺延为 **P7**。同步阶段索引、遗留归属、D8 复验和打包引用。P6 详案见 [`P6_NATIVE_UI_DESIGN.md`](P6_NATIVE_UI_DESIGN.md)；当前只完成规划，未实施界面。 |

---

## 0. 总体判断

| 项目 | 值 |
|---|---|
| 难度 | 7 / 10（v1 为 6.5，MVVM 层重写使其上升） |
| 需重写 | ≈ 13.8k 行（ViewModel 6.0k + XAML 4.3k + code-behind 2.5k + 胶水 1.0k） |
| 直接复用 | ≈ 12.5k 行（Core 8.9k + Texts.Designer 3.6k） |
| 总工时 | **24.5 ~ 36 人日**（8 阶段，每阶段产出可运行 exe；原迁移基线 19.5 ~ 28 人日，新增 P6 暂估 5 ~ 8 人日，待设计落地后复估） |
| 误差 | ±40% |

### 复用/重写盘面

| 层 | 行数 | 处置 | 阶段 |
|---|---:|---|---|
| `FireAxe.Core` | ~8,900 | **零改动复用**（0 处 ReactiveUI，仅 `INotifyPropertyChanged`） | — |
| `Resources/Texts.Designer.cs` + resx | 3,575 | **零改动复用** | 1 |
| `ViewModels/`（44 个文件） | 6,057 | **整体重写**（ReactiveUI → CommunityToolkit.Mvvm） | 1 |
| `AppSettings.cs` | 755 | 大部分照搬，`ReactiveObject`/`WhenAnyValue`×3/`ReactiveCommand`×1 需翻译 | 1 |
| `Views/*.axaml`（47 个） | 4,286 | **全重写** | 3–5 |
| `Views/*.axaml.cs`（45 个） | 2,450 | **全重写**（逻辑可照搬） | 3–5 |
| 胶水（消息框/转换器/窗口管理/启动/主题） | ~1,000 | **全重写** | 2 |

### 为什么放弃 ReactiveUI（决策记录）

1. 版本断层：源工程 `ReactiveUI 22.2.1`（经 `ReactiveUI.Avalonia 11.3.8`），WinUI 侧最新 `ReactiveUI.WinUI 26.0.1`，跨四个大版本，API 兼容性需赌。
2. 生态方向：WinUI 3 官方 MVVM 指引是 `CommunityToolkit.Mvvm`（源生成器、零运行时开销、微软维护）。
3. 依赖收敛：去掉 ReactiveUI + System.Reactive 两棵大树，安装包与启动开销都受益。
4. 副作用是好事：57 个 `Interaction<T,U>` 将改造为显式的 `IDialogService` / `IPickerService` / `IWindowService`，**ViewModel 从此零 UI 框架依赖**，将来若还要换壳（WPF / MAUI / Web），VM 层可直接搬走。

**代价**：+2.5 ~ 4 人日，且是全项目最大的"行为漂移"风险来源（见 R1/R8/R9）。

---

## 1. 目标工程布局

```
<WORKSPACE_ROOT>\
├─ FireAxe_Alt\                    # 【临时基线 / 只读参考】不修改、不回写；
│                                  #   WinUI3 版完成后整体废弃（含其 .git 历史悠久保留即可）
├─ PORTING_PLAN.md                 # 本文档
└─ FireAxeWinUI3\                  # 【全新项目 · 全新仓库 · 全新 .git 历史】
   ├─ .git\                        # 从 init 开始，不继承 Alt 的任何提交
   ├─ .gitattributes / .gitignore / LICENSE.txt / README.md
   ├─ Directory.Build.props        # 版本号从 0.0.0 起（全新项目）
   ├─ .github\workflows\           # CI：build + test + zip + 证据上传（2026-11-14 修复干净检出构建，远端时长达标前仍 🟡）
   ├─ .github_disable\             # 已停用的简版 build.yml，历史存档
   ├─ FireAxe.sln
   ├─ FireAxe.Core\                # 从 Alt 拷入（一次性基线），此后独立演进
   └─ FireAxe.WinUI3\              # 应用工程（RootNamespace = FireAxe，AssemblyName = FireAxe）
      ├─ App.xaml / App.xaml.cs
      ├─ Views\                    # 与 Alt 同名，便于对照
      ├─ ViewModels\               # 重写，保持命名空间 FireAxe.ViewModels
      ├─ Services\                 # 新增：对话框 / 选择器 / 窗口 / 剪贴板 / 图片 / 调度
      ├─ Converters\
      ├─ Resources\                # Texts.resx / Texts.zh-Hans.resx（原样拷入）
      ├─ Assets\
      └─ ExportedAssets\
```

### 三条硬约定

1. **命名空间保持 `FireAxe` / `FireAxe.Views` / `FireAxe.ViewModels` 不变。**
   ViewModel 可逐文件对照重写；`Texts.Designer.cs` 与两个 resx 也原样复用。
   但 **XAML 取值写法必须换**：`{x:Static r:Texts.Xxx}` →
   `{x:Bind r:Texts.Xxx, Mode=OneTime}`（WinUI 3 没有 `x:Static` 标记扩展，
   详见 `phase-notes/P0-spike.md` §S2）。

2. **`FireAxeWinUI3` 是一个全新项目**：全新仓库、全新 git 历史、自己的 LICENSE/README/CI/版本号。
   `FireAxe_Alt` 只作为**一次性基线**被读取（拷 Core、对照 XAML/VM），移植完成后即废弃。
   因此**不做双向同步**：迁移期间发现的问题，一律只在新工程修。

3. **每个 Phase 结束都必须产出一个能双击运行的 exe。** 不做"全部写完再编译"。

### 工程参数（草案）

```xml
<TargetFramework>net10.0-windows10.0.26100.0</TargetFramework>
<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>
<WindowsPackageType>None</WindowsPackageType>   <!-- 先非打包，省 MSIX 折腾 -->
<UseWinUI>true</UseWinUI>
<EnableMsixTooling>false</EnableMsixTooling>
<Nullable>enable</Nullable>
<LangVersion>latest</LangVersion>                <!-- P0 新增：partial property 需要 -->
```

依赖：`Microsoft.WindowsAppSDK 2.6.5`（已缓存）、`Microsoft.Windows.SDK.BuildTools 10.0.26100.9457`（已缓存）、
**`CommunityToolkit.Mvvm 8.4.2`（需联网还原）**。
**不引入** ReactiveUI / System.Reactive（个别必要处再单独评估，见 §7 D7）。

> **P0 实测：`CommunityToolkit.Mvvm` 必须用 8.4.2，不能用 8.4.0** ——
> 8.4.0 的生成器在 `LangVersion=latest`（C# 14）下产出不了 partial property 的实现（CS9248），
> 而 WinUI 3 又必须用 partial property（否则 MVVMTK0045）。见 P0-spike.md。

---

## 2. 阶段总览

| 阶段 | 内容 | 工时 | 累计 | 交付物 |
|---|---|---:|---:|---|
| **P0** | 可行性验证（Spike） | 0.5 ~ 1d | 1d | 跑得起来的空壳窗口 + 验证结论 |
| **P1** | 平台抽象层 + ViewModel 重写 ✅ | 4 ~ 6d | 7d | VM 层零 UI 依赖、编译通过、单测冒烟（**已交付，见 notes**） |
| **P2** | 应用骨架与外壳 ✅ | 2.5 ~ 3d | 10d | 主窗口菜单可用、对话框可弹、设置可存（**已交付，见 notes**） |
| **P3** | 主界面（Explorer + AddonNodeView） | 5 ~ 8d | 18d | **核心功能可用**：开目录/浏览/启停/改名/移动/搜索 |
| **P4** | 工具窗 · 第一批（简单、同构） | 2 ~ 3d | 21d | 14 个中小窗口 |
| **P5** | 工具窗 · 第二批（复杂、数据密集） | 3 ~ 4d | 25d | 7 个重窗 |
| **P6** | Windows 原生界面重设计（Windows 11 设置风格） | 5 ~ 8d | 33d | 新导航外壳、设置卡片、统一主界面/工具窗及界面验收证据 |
| **P7** | 收尾（交互/性能/打包/回归，原 P6） | 2.5 ~ 3d | 36d | 功能对齐 v0.7.6，可分发 |

> P6 为新增工作量，5 ~ 8 人日是规划估算，不是实测；累计按各阶段上限估算。P0～P5 的既有完成状态保持原结论。

| | Phase Notes |
|---|---|
| P0 | [`phase-notes/P0-spike.md`](phase-notes/P0-spike.md) |
| P1 | [`phase-notes/P1-abstraction.md`](phase-notes/P1-abstraction.md) |
| P2 | [`phase-notes/P2-shell.md`](phase-notes/P2-shell.md) |
| P3 | [`phase-notes/P3-main-view.md`](phase-notes/P3-main-view.md) |
| P4 | [`phase-notes/P4-tool-windows.md`](phase-notes/P4-tool-windows.md) |
| P5 | [`phase-notes/P5-data-dense-windows.md`](phase-notes/P5-data-dense-windows.md) |
| P6 | [实施记录](phase-notes/P6-native-ui.md)；首轮代码已落地，实机验收未完成 |
| P7 | 原 P6 收尾；开工时从 `_TEMPLATE.md` 创建结论文档，完成后入索引 |

> **约定**：每个阶段收尾必须产出一份 `docs/phase-notes/P<n>-<主题>.md`，
> 与当阶段代码进同一个提交。格式、分级、索引规则见
> [`docs/phase-notes/README.md`](phase-notes/README.md)。
> 未写 notes 的阶段不算完成。

---

## 3. 阶段详解

### P0 · 可行性验证（0.5 ~ 1 天）✅ **已完成 2026-11-08**

> **实测结论见 `phase-notes/P0-spike.md`。S1~S5 全部通过，未使用任何退路。**
> 下面保留原验证项表格作为记录，实测细节以 P0-spike.md 为准。
>
> 已交付：新仓库（`8b63c39`，96 文件）+ `FireAxe.Core` 零改动编译通过 +
> 可运行的 WinUI 3 壳窗口 + `phase-notes/P0-spike.md`。
>
> 原本列为「待人工确认」的 Picker 返回路径，已补测通过
> （`Microsoft.Windows.Storage.Pickers.FileOpenPicker` 在非打包模式下返回真实路径）。**P0 无残留项。**

**目标：把新仓库立起来，并先用最小代价排掉可能推翻方案的未知项。**

**准备步骤（半天内完成）**

1. `cd FireAxeWinUI3` → `git init`（全新历史，不继承 Alt 任何提交）
2. 拷入并适配：`.gitattributes`、`.gitignore`（补 WinUI 3 / VS 规则：`obj/`、`bin/`、`*.user`、`AppPackages/`）、`LICENSE.txt`（Apache-2.0，沿用）
3. 重写 `README.md`（WinUI 3 版说明 + 构建方式），新建 `Directory.Build.props`（版本号 **0.0.0**）
4. 拷贝 `FireAxe.Core` → `FireAxeWinUI3\FireAxe.Core\`，独立编译通过
   （需还原 `Downloader` / `ValvePak` / `ValveKeyValue` / `Newtonsoft.Json` / `Serilog`）
5. 建立 `FireAxe.sln`（`FireAxe.Core` + `FireAxe.WinUI3` 两个工程）+ 最小 WinUI 3 空窗口
6. 首次提交（`chore: scaffold WinUI 3 project from FireAxe_Alt 0.7.6 baseline`）

**验证项**

| # | 验证项 | 通过标准 | 失败退路 | 实测结果 |
|---|---|---|---|---|
| S1 | `CommunityToolkit.Mvvm 8.4` + WinUI 3 + .NET 10：`[ObservableProperty]` / `[RelayCommand]` 源生成器正常，绑定能刷新 | 窗口绑一个 VM，按钮命令 + 属性变更都生效 | 极低概率；退手写 INPC | ✅ 通过（但须用 8.4.**2** + partial property，见 R11/§3 P1.1） |
| S2 | resx + `x:Static` 在 XAML 与 C# 中均可解析 | 窗口标题显示 `Texts.File` 的中英文 | ~~改用 `{x:Uid}` + `.resw`（+2d）~~ **作废** | ✅ 通过，但 **`x:Static` 不存在**，改 `{x:Bind r:Texts.X, Mode=OneTime}`，退路不需要 |
| S3 | 非打包模式下 `ContentDialog` / `FileOpenPicker` / `FolderPicker` 可用 | 能弹出并返回结果 | 统一 `InitializeWithWindow` 辅助；退回 P/Invoke 原生对话框 | ✅ 通过（新 `Microsoft.Windows.Storage.Pickers` 免 HWND；返回路径留 P2 验） |
| S4 | `ListView` 虚拟化在 2000 项 + 三种 `ItemTemplate` 下的滚动表现 | 无明显卡顿 | 退回 `ListView` + `ItemsStackPanel`，砍瓦片视图 | ✅ 通过（2000 项仅实体化 50 容器，30 次跳转 74 ms） |
| **S5** | **`{x:Bind}` 与 `[ObservableProperty]` 配合正常（编译期绑定，性能优于 `{Binding}`）** | 派生属性 / 命令 / 集合都能绑 | 全面退回 `{Binding}`（可用，只是慢一点） | ✅ 通过（属性 / 派生属性 / int / 命令全绿） |

**产物**：`phase-notes/P0-spike.md`（结论 + 决定性代码片段）

---

### P1 · 平台抽象层 + ViewModel 重写（4 ~ 6 天）✅ **已完成 2026-11-08**

> **实测结论见 `phase-notes/P1-abstraction.md`。41 个 VM 全部落地，编译 0 警告 0 错误，
> 9 个冒烟测试全绿，exe 可运行。**
>
> 与原计划的偏差（以 notes 为准）：
>
> 1. 抽象层归并成 **3 个服务 25 个方法**（原估 ~15）：`IWindowService` 吸收了 `IAppWindowManager`
>    的 11 个开窗方法，另加 `ShowChoiceAsync`（更新提示的"前往下载/忽略"）。
> 2. 新增 `IAppThemeService`（第 6 个之外的补充抽象），承接 `AppSettings.ColorTheme` 对
>    `Application.RequestedThemeVariant` 的依赖。
> 3. 命名嵌套 VM 的构造签名按 §6.3 保持原样，新增静态 `AppServices` 服务定位桥（见 R13）。
> 4. 对话框文案从 View 搬进 VM，并抽出 `RedundantVpkFileDialogs` / `MessageComposer` 两个去重件。
> 5. 冒烟测试落在新建的 `FireAxe.Tests`（xunit，可直接引用 WinUI 应用工程），实际 9 个用例。
> 6. `TileViewSize` 用占位常量 200（Semi 的 `size_addon_tile*` 不在仓库内），P3 定真值。

#### P1.0 先立地基（0.5 天）

- `Services/Abstractions/`：`IUiDispatcher`、`IImageLoader`、`IClipboardService`、`IDialogService`、`IPickerService`、`IWindowService`
- 这些接口**只依赖 BCL**，实现放 `Services/Impl/`（WinUI 侧）
- **实现 `IPickerService` 时优先用 `Microsoft.Windows.Storage.Pickers`**（WinAppSDK 1.8+，构造时吃
  `AppWindow.Id`，免 `WindowNative.GetWindowHandle` + `InitializeWithWindow`）；
  传统 `Windows.Storage.Pickers` 留作退路。P0 已验证两条路径都能初始化，见 P0-spike.md。
- 把 57 个 `Interaction<T,U>` 归并成 **3 个服务、约 15 个方法**：

| 原 Interaction 类型 | 归入 |
|---|---|
| `ConfirmDelete*`、`Show*Exception*`、`Show*Error*`、`Show*Success*`、`Show*Failed*`（约 35 个） | `IDialogService`：`ConfirmAsync` / `ShowErrorAsync` / `ShowInfoAsync` / `InputTextAsync` |
| `Choose*Directory`、`Choose*File`、`SaveAddonRootFile`、`SelectCustomImagePath`（5 个） | `IPickerService` |
| `ShowImportResult`、`ShowNewWorkshopCollectionWindow`、`ShowDeletionProgress` 等（约 5 个） | `IWindowService`（与既有 `IAppWindowManager` 合并） |

> 净收益：View 侧 57 处 `RegisterHandler` 注册代码**全部消失**（原本每加一个窗口都要复写一遍）。

#### P1.1 翻译规则表（贯穿全工程）

| ReactiveUI | CommunityToolkit.Mvvm | 坑 |
|---|---|---|
| `ReactiveObject` / `ViewModelBase` | `ObservableObject`（源生成器基类） | 顺手获得 `SetProperty`/`OnPropertyChanged`。**⚠️ 实测：基类必须全限定或经 `ViewModelBase` 继承，否则撞 `FireAxe.ObservableObject`（静默失效，见 R11）** |
| 可观察属性 | `[ObservableProperty] public partial T X { get; set; }` | **⚠️ 实测：WinUI 3 下只能用 partial property**（字段写法报 MVVMTK0045）；初值不能写在属性上（CS8050），放构造函数 |
| `ReactiveCommand.Create(...)` **93 处** | `[RelayCommand]` + `RelayCommand` | **`CanExecute` 若依赖可观察属性，必须手动 `NotifyCanExecuteChanged()`** —— 这是最容易漏的地方。**⚠️ 实测：改用 `[NotifyCanExecuteChangedFor(nameof(XxxCommand))]` 挂到属性上即可声明式解决，不用手写（R8 大幅下调）** |
| `ReactiveCommand.CreateFromTask(...)` **97 处** | `[RelayCommand]` + `AsyncRelayCommand` | CT 默认**允许并发重入**；源工程的命令语义需逐个确认（是否应禁止重入 / 支持取消） |
| `this.WhenAnyValue(x => x.A, x => x.B).Select(...).ToProperty(...)` **124 + 25 处** | `[ObservableProperty]` + `partial void OnAChanged(...)` + 手写派生属性 | 多属性联动需手写；注意初值、去重、更新顺序。**⚠️ 实测：派生属性不会自动通知，必须显式挂 `[NotifyPropertyChangedFor(nameof(X))]`** |
| `Interaction<T,U>` **57 处** | `IDialogService` / `IPickerService` / `IWindowService` | 见 P1.0 |
| `IActivatableViewModel` + `WhenActivated` **21 个 VM** | 显式生命周期：DI 单例 + `LoadAsync()` / `DisposeAsync()` | **语义变化点**：原先"视图激活即激活"，现在要由窗口/视图显式驱动 |
| `DisposeWith` / `CompositeDisposable` **148 / 74** | `CancellationTokenSource` + 普通事件解绑 | 不引入 Rx 后需改用 BCL 机制，注意事件泄漏 |
| `ObservableCollection` 用法 | 不变（BCL 类型） | — |
| `{x:Static r:Texts.Xxx}`（XAML） | `{x:Bind r:Texts.Xxx, Mode=OneTime}` | **⚠️ 实测：WinUI 3 无 `x:Static` 标记扩展（WMC0001）**；机械替换即可，`Texts.Designer.cs` 不用改 |

另有一条与 VM 无关但会炸编译的：**出现在 `x:DataType` / `ItemTemplate` 里的数据类，属性不能用 `init`**
（生成的 `XamlTypeInfo.g.cs` 需要 setter，报 CS8852）。

#### P1.2 分批重写（按翻译工作量排序，先易后难）

| 批次 | 文件 | 工作量分*(命令+WhenAny+ToProperty+Interaction+激活) |
|---|---|---:|
| **批 1 地基**（~0.5d） | `ViewModelBase`、`AddonNodeComparer`、`AddonNodeSortMethod`、`AddonNodeEnableState`、`UpdateRequestReply`、`AddonGroupViewModel` 等纯逻辑/极简文件 | ~5 |
| **批 2 简单窗**（~1.5d） | 分数 ≤ 10 的 22 个 VM：`DownloadItemList`、`VpkAddonConflictList`、`AddonProblemList`、`AddonNodePicker`、`OperationsProgress`、`FileNameFixer`、`ReplacementOverview`… | ~90 |
| **批 3 中等窗**（~2d） | `AddonTagManager`(29)、`FileCleaner`(27)、`AppSettings`(21)、`AddonNodeSimple`(18)、`AddonTagEditor`(16)、`WorkshopVpkAddon`(15)、`AddonNameAutoSetter`(14)、`DownloadItem`(11)、`WorkshopVpkFinder`(9)… | ~175 |
| **批 4 重窗**（~2d） | **`MainWindowViewModel`(107)**、**`AddonNodeExplorerViewModel`(90)** | ~197 |

> 两个重窗占了近 40% 的翻译量，且是主链路，**必须放在批 4 单独做、单独验收**。

**验收**：`dotnet build` 通过；VM 层不出现 `ReactiveUI` / `Avalonia` 符号（`Select-String` 自查）；
为主链路 VM（批 4）补 3~5 个冒烟单测（`FireAxe.Core` 与 VM 均无 UI 依赖，可直接测）。

---

### P2 · 应用骨架与外壳（2.5 ~ 3 天）✅ **已完成 2026-11-08**

**目标：跑通"启动 → 主窗口 → 各种对话框 → 设置持久化 → 正常退出"。**

| 子项 | 源文件 | 说明 |
|---|---|---|
| 启动与生命周期 | `Program.cs`、`App.axaml(.cs)` | Serilog、`TaskScheduler.UnobservedTaskException`、CrashReporter |
| DI 容器 | `App.axaml.cs` L54-82 | 保持 `Microsoft.Extensions.DependencyInjection`；新增服务注册 |
| 单实例 | `AppMutex.cs` | 改用 `AppLifecycle.AppInstance.FindOrRegisterForKey` + `RedirectActivationTo` |
| 窗口管理 | `AppWindowManager.cs`（182 行） | 重写；WinUI 里"最后一个窗口关闭即退出"，需显式保持主窗口引用 |
| 对话框体系 | `CommonMessageBoxes.cs`（267 行） | 实现 `IDialogService`：统一封装 `ContentDialog`，处理 `XamlRoot` 与"模态套模态" |
| 主题与资源 | `App.axaml`(104)、`CommonResources.axaml`(47)、`Icons.axaml`(21) | 54 个 `Selector=` 样式 → Fluent 隐式样式 + 少量 `x:Key`；自定义色 → 独立 `ResourceDictionary`；18 个 `StreamGeometry` → `Path Data` 或 Segoe Fluent Icons |
| 布局补件 | `DockPanel` 74 处 / `WrapPanel` 7 处 | 优先用 Community Toolkit 现成实现（遵循"用库不重写"约定）；确无则自写并注明理由 |
| View 定位 | `ViewLocator.cs` | WinUI 用隐式 `DataTemplate`（`App.xaml` 的 `ResourceDictionary`）或 `IViewFor` 注册表，P2 定死一种 |
| 保存调度 | `SaveManager.cs` | `DispatcherTimer` → `DispatcherQueueTimer` |
| 关闭流程 | `App.axaml.cs` L86-127 | 异步 Dispose + 15 秒兜底强杀，逻辑照搬 |
| 设置持久化 | `AppSettings.cs`（755 行） | 主体照搬；`ReactiveObject`→`ObservableObject`；窗口宽高/最大化 → `AppWindow` 物理像素 + DPI 换算 |

**验收**：空主窗口 + 完整菜单树；中英文可切换；确认框/异常框/输入框/文件夹选择/文件选择/进度窗全部可弹；
关闭后 `%Documents%\FireAxe\settings.json` 与 Alt 版内容一致。

> **实际结果（2026-11-08）**：菜单树 / 中英文切换 / 确认框 / 异常框 / 信息框 / 多按钮选择框 / 文件夹选择 / 进度窗 /
> 设置逐字节往返 / 关闭流程全部实机通过；**输入框与 Skip·SkipAll·Abort 框在 P2 没有可达 UI 入口**（调用方在 P3/P4 窗口里），
> **文件选择器未取得界面证据**，两项均标 🟡 顺延到 P3 首次使用处复验。
> 详细证据、6 个坑与 8 条硬约束见 [`phase-notes/P2-shell.md`](phase-notes/P2-shell.md)。

---

### P3 · 主界面（5 ~ 8 天）✅ **已完成 2026-11-08**（结论见 `phase-notes/P3-main-view.md`）

**目标：达到"日常可用"——这是判断移植成败的唯一标准。**

#### 涉及文件（11 个视图 + 1 个自定义控件）

| 文件 | XAML | code-behind | 关键难点 |
|---|---:|---:|---|
| `AddonNodeExplorerView` | 429 | 239 | **三种视图形态 + 选择模型 + 键盘 + 上下文菜单** |
| `AddonNodeView` | 304 | 105 | 详情面板 + 高级面板 + 内联改名 |
| `EditableTextBlock` | 67 | 244 | **自定义控件：双击编辑 / 校验 / Enter·Esc** |
| `AddonNodeGridRowView` | 74 | 11 | 列表行样式（列对齐、标签、图标） |
| `AddonNodeTileView` | 58 | 9 | 瓦片（`AddonTileSize` 动态尺寸） |
| `WorkshopVpkAddonSectionView` | 148 | 50 | 工坊信息 + 下载状态 + 图片 |
| `RefAddonNodeSectionView` | 68 | 63 | 引用源选择 |
| `VpkAddonSectionView` | 65 | 20 | VPK 信息 |
| `AddonNodeDependenciesView` | 52 | 54 | 依赖列表增删 |
| `AddonNodeEnableButton` | 29 | 57 | 三态启用按钮 |
| `AddonIconView` | 24 | 40 | 图片异步解码 + 缓存（→ `IImageLoader`） |
| 其余小件：`AddonNodeNavBarView` / `ItemView` / `AddonNodeSectionViewDecorator` / `AddonTagView` / `AddonTagCheckBox(…)` / `AddonGroupSectionView` / `AddonRefView` | 小 | 小 | 逐个搬 |

#### 必须先定死的 3 个设计决策

1. **选择模型**：改用 `ListView`/`GridView` 原生 `SelectedItems` + `SelectionMode=Multiple`
   （`SelectionModelHelper` 作废），还是 `ItemsRepeater` + `SelectionModel`？→ **建议先 `ListView`**。
2. **三种视图形态**：一套 `ListView` + 三套 `ItemTemplate`（推荐），还是三个独立控件？
3. **双击组进入 + 多选时序**：WinUI 的 `DoubleTapped` 与 `SelectionChanged` 顺序与 Avalonia 不同，
   需实测"双击进入组时不破坏原选择"。

> **P3 定案（2026-11-08）：**
> ① 用 `ListView` + **`SelectionMode=Extended`**（`Multiple` 会画复选框，语义不符）；`SelectedItems` 不是依赖属性、**不能绑定**，
> 改为 View 侧手工双向同步（`SelectionChanged` → `SelectNodes`；`SelectedNodes.CollectionChanged` → 回写两个 ListView，`_syncingSelection` 防回环）。
> ② 用**两个并列 `ListView`**（瓦片 / 表格）切 `Visibility`，瓦片用 `ItemsWrapGrid`（原生虚拟化），标签与移动条用自写 `WrapPanel`。
> ③ 双击不会破坏选择语义（进入组后 VM 主动清空选择），但会**让焦点逃出 Explorer** —— 见 R22，必须加窗口级按键 fallback。

#### 交互清单（不可遗漏）

- `Ctrl+F` 聚焦搜索 / `Delete` 删除 / `F2` 改名 / `Ctrl+X`·`Ctrl+V` 移动 / `Enter`·`Esc` 提交取消
- 鼠标侧键（`XButton1`）返回父组 —— WinUI 需读 `PointerPointProperties.IsXButton1Pressed`
- 双击组进入；`AddonNodeView` 折叠时列宽归零
- 右键上下文菜单（8 处）：新建 / 删除 / 移动 / 引用 / 打开工坊页 / 自定义图片……
- 搜索选项面板（排序 8 种、标签过滤、扁平化显示）

**验收（真实环境）**：开真实插件库 → 三视图切换 → 多选 → 启停（含组递归）→ 改名 → 导入新 VPK →
搜索/排序/标签筛选 → 推送 → 游戏内可见。

---

### P4 · 工具窗 · 第一批（2 ~ 3 天）

结构高度同构（表单或单列表），可批量模板化。

| 窗口 | XAML | code-behind | 备注 |
|---|---:|---:|---|
| `AppSettingsWindow` | 475 | 41 | 最大，但全是表单；`Expander` 分组 + 绑定 |
| `AddonNameAutoSetterWindow` | 181 | 76 | 任务列表 + 进度 |
| `AddonProblemListWindow` | 149 | 30 | 问题分组列表 |
| `VpkAddonConflictListWindow` | 133 | 44 | 列表 + 详情联动 |
| `DownloadItemListWindow` + `ItemListView` + `ItemView` | 15+39+95 | 11+51+18 | 进度条 + 定时刷新 |
| `FileNameFixerWindow` | 84 | 28 | 表单 + 结果列表 |
| `AddonTagManagerWindow` | 62 | 48 | 增删改 |
| `AddonTagEditorWindow` | 55 | 52 | 对话框 |
| `NewWorkshopCollectionWindow` | 52 | 52 | 对话框 + 进度 |
| `AddAddonTagWindow` | 46 | 34 | 输入对话框 |
| `AddonImportResultWindow` | 45 | 11 | 结果列表 |
| `AddonNodeCustomizeImageWindow` | 44 | 56 | 图片预览 + 选择 |
| `AddonNodePickerWindow` | 32 | 42 | 树选择 |
| `AboutWindow` / `CheckingUpdateWindow` / `AppAlreadyRunningMessageWindow` | 88/18/19 | 11/9/11 | 极简 |

---

### P5 · 工具窗 · 第二批（3 ~ 4 天）🟡 **有条件完成 2026-11-10**

> 结论见 [`phase-notes/P5-data-dense-windows.md`](phase-notes/P5-data-dense-windows.md)：三个重窗及路由已实现，Debug 构建 0 警告/错误、全测 9/9、exe 可启动；逐窗交互及 D8 符号链接推送复验交 P7，不视为已通过。

| 窗口 | XAML | code-behind | 难点 |
|---|---:|---:|---|
| `FileCleanerWindow` | 207 | 72 | 分组勾选 + 递归统计 + 大列表 |
| `VpkAddonConflictingDetailsView` | 192 | 32 | 冲突矩阵（双向列表联动） |
| `ReplacementOverviewWindow` | 152 | 41 | 槽位卡片网格 + 分类折叠 |
| `WorkshopVpkFinderWindow` | 98 | 44 | 扫描 + 结果表 |
| `OperationsProgressWindow` + `View` + `MessageTemplate(s)` | 28+54 | 42+98 | 通用进度窗，被多处复用 |
| `CrashReporter` / `ObjectExplanation*` / `ExceptionExplanation*` / `AddonProblemExplanations` | ~250 | — | 纯逻辑照搬（11 个问题类型说明映射） |

---

### P6 · Windows 原生界面重设计（5 ~ 8 天，新增）🟡 **实施中，待实机验收**

**目标：从旧界面的 WinUI 控件迁移，推进到 Windows 11 设置风格的信息架构与交互设计。** 首轮导航、设置分类、浏览详情与工具窗代码已落地；实施覆盖见 [P6-ui-map.md](P6-ui-map.md)，阶段记录见 [P6-native-ui.md](phase-notes/P6-native-ui.md)。详细要求及验收清单见 [`P6_NATIVE_UI_DESIGN.md`](P6_NATIVE_UI_DESIGN.md)。

- **主窗导航**：原生侧边导航、页面标题与说明、任务命令区；高频操作可见，低频操作进入溢出菜单，保留全部功能入口。
- **设置重设计**：分类导航、设置卡片、标题/说明/控件、关联子项展开；优先评估兼容版本的现成 SettingsCard / SettingsExpander 能力。
- **浏览与详情**：统一搜索/筛选/视图工具区、主从信息层级与状态反馈；保留多选、快捷键、数据密度和列表虚拟化。
- **工具窗与对话框**：统一页头、间距、主题资源、主次命令、空态、进度和异常；独立窗口可按任务保留。
- **原生体验**：系统字体与图标、主题卡片、深浅色/高对比度、响应式布局、可降级的 Mica/标题栏，以及键盘与辅助功能。
- **交付**：可运行 exe、全部旧入口的去向清单、实际改版前后截图与操作证据、七段式 `phase-notes/P6-native-ui.md`；P6 对改动范围内的行为回归负责。

> **边界**：允许重设计布局、导航和视图组织，Core 业务规则与文件格式保持原语义；不以调色、圆角或材质替代信息架构改版。P3～P5 的旧遗留与最终全量回归归 P7，P6 若提前验证/修复，需留证据并同步记录。

### P7 · 收尾（2.5 ~ 3 天，原 P6）

- **观感验收**：在 P6 设计基础上检查全窗口一致性、深浅色/高对比度及材质降级，处理残留视觉问题；主体设计归 P6。
- **DPI 与大屏**：主窗口尺寸记忆（`AppWindow` 物理像素 ↔ DIP）、多显示器居中
- **快捷键总表**复核（对照 11 处 `e.Key` / `KeyModifiers` 分支）
- **性能**：2000+ 插件滚动、图片解码缓存、虚拟化
- **打包**：默认非打包 zip；可选 MSIX（`EnableMsixTooling` + 自签名证书）
- **CI**：对照 `FireAxe_Alt\.github\workflows\build-and-release.yml`，改 `dotnet publish -r win-x64`（2026-11-14 已修掉远端构建阻断：`app.manifest` 被 `.gitignore` 的 `*.manifest` 规则排除，干净检出必报 CS1926）
- **回归**：主链路 + 全窗口各一遍（对照 §5）
- **文档**：README / CHANGELOG

---

## 4. 风险登记册

| ID | 风险 | 概率 | 影响 | 应对 |
|---|---|---|---|---|
| **R1** | **VM 重写引入行为漂移**（最可能出问题的地方） | **高** | **高（+3~5d）** | 逐文件对照重写、不顺手重构；批 4 两窗单独验收；主链路补冒烟单测 |
| **R8** | `CanExecute` 可观察性漏翻译 → 按钮该灰不灰、该亮不亮 | **中**（P0 由高下调） | 中 | 逐个 `[RelayCommand]` 核对 `CanExecute`；**用 `[NotifyCanExecuteChangedFor]` 声明式挂到属性上**（P0 实测有效，不用手写通知）；§5 清单保留"禁用态"专项检查 |
| **R9** | **VM 生命周期语义变化**（`IActivatableViewModel` 21 个） | 中 | 中 | 统一为"DI 单例 + 显式 `LoadAsync`/`DisposeAsync`"，P2 定死一套模式再动 P3 |
| R2 | `ListView` 虚拟化在多选 + 三模板下性能不达标 | **低-中**（P0 由中下调） | 中（+2d） | P0 实测 2000 项仅实体化 50 容器、30 次跳转 74 ms，方案可用；P3 按真实插件库（图片解码负载）复测；仍可简化瓦片视图 |
| R3 | 54 处 `Selector=` 样式语义无法一一还原 | 高 | 低（外观差异） | 接受观感变化，Fluent 原生优先 |
| R4 | 非打包模式下 Picker/剪贴板需 HWND 初始化 | **低**（P0 由高下调） | 低（+0.5d） | P0 实测：新 `Microsoft.Windows.Storage.Pickers` 吃 `AppWindow.Id`，**不需要 HWND**；剪贴板往返正常 |
| R5 | 阻塞操作落在 UI 线程导致卡顿 | 中 | 中 | `IUiDispatcher` 提供 `Low` 优先级映射 |
| R6 | 无自动化测试，回归只能手工 | **确定** | 中（+3~4d） | P1 起给 Core + 批 4 VM 加冒烟测试；UI 用截图自查 |
| R7 | 本地化热切换需重建绑定 | 中 | 低 | 切换后重启窗口（可接受）。P0 已实测确认此行为 |
| **R10** | **过渡期"两个版本都在维护"的错觉** —— 在 Alt 侧顺手修 bug 会产生两份实现 | 低 | 低 | 硬性约定：迁移期所有修复只在新工程做；`FireAxe_Alt` 只读不改 |
| **R11** | **`FireAxe.Core` 的 `ObservableObject` 与 CommunityToolkit 的撞名**，导致源生成器**静默**失效（不报错，只有下游一堆 "名称不存在"） | **高（已发生）** | 中（排查耗时） | **所有 VM 一律继承 `ViewModelBase`，不得直接继承 `ObservableObject`**；`ViewModelBase` 内部用全限定基类名一次性吸收该冲突。见 P0-spike.md 坑 1 |
| **R12** | `CommunityToolkit.Mvvm` 版本/语言版本组合不当 → 整个 VM 层编译不过（CS9248） | 中 | 中 | 锁死 `8.4.2` + `LangVersion=latest`；若被迫降级须同步改 `preview` |
| **R13** | **`AppServices` 静态服务定位器的初始化顺序**：嵌套 VM 用 `new` 构造（§6.3 禁止改签名），只能从静态入口取服务；若 `Provider` 晚于 VM 构造，功能**静默失效**（不弹框、不定时器） | **中（P1 新增）** | 中 | `App` 构造函数里**先**赋 `AppServices.Provider` 再解析任何 VM；P2 落地 DI 时用 §5 第 2 条核查 |
| **R14** | **XamlCompiler 把真实 XAML 错误吞成 `WMC9999`**（伴生 "ErrorMessages.resources" 提示），误判成本高 | **中（P1 新增，已发生）** | 中（≥半天误判） | 见 P1-abstraction.md §3.7：见到该组合即怀疑"某个 `{x:Bind}` 成员名不存在"，逐个核对；**不要怀疑 SDK 安装**，改 `VSLANG` 无效 |
| **R15** | `AsyncRelayCommand` **默认禁止并发重入**，与 Rx `CreateFromTask` 语义不同 | **低-中（P1 新增）** | 低 | 逐个命令确认原行为；需要并发时显式传 `AsyncRelayCommandOptions.AllowConcurrentExecutions` |
| **R16** | **关闭顺序陷阱**：先 `CloseAllWindows()` 再 `DisposeAsync()`，VM 的 `Dispose` 会去碰已销毁的 `AppWindow`（`COMException 0x800710DD`），且只在退出瞬间暴露 | **中（P2 新增，已发生并修复）** | 中（关不上 / 静默异常） | 每个 Window 在 `Closed` 里置 `_isClosed` 并退订 `PropertyChanged`；见 P2-shell.md §3.9 |
| **R17** | **双输出树**：直接 build csproj 落 `bin\Debug`，build 解决方案落 `bin\x64\Debug`，导致“改了没生效”的误判 | **中（P2 新增，已发生并修复）** | 中（≥2h 误判） | csproj 固定 `<Platform>x64</Platform>`；运行只认 `bin\x64\Debug` |
| **R18** | **`ContentDialog` 长文本不换行不滚动**：异常框带调用栈时宽到超出窗口被裁 | **中（P2 新增，已发生并修复）** | 低 | 正文一律包 `ScrollViewer(MaxWidth 480, MaxHeight 320)` + `Wrap` |
| **R19** | **图标机制**：`PathIcon.Data` **不能绑定**（`{x:Bind}`/`{Binding}` 到 `Geometry` 启动即抛 `ArgumentException`）；`XamlReader` 解析出的 `Geometry` **不能迁移**给第二个元素（PathIcon / Path / GeometryGroup 全部 FAILED）；`<PathGeometry Figures="{StaticResource …}">` 报 `WMC0055` | **确定（P3 已发生）** | 高（启动即崩） | 路径数据一律写成 `Views/Icons.cs` 的 `string`，用 `ctl:IconView`（`ContentControl` + `XamlReader` 造 `Path` 直接当 Content）。见 `P3-main-view.md` §3.3 |
| **R20** | **派生 VM 用 `new` 遮蔽基类同名属性** → WinUI 反射式 `{Binding}` 路径解析**静默失败**（无异常无日志），整条 `Addon.*` 下游绑定全废 | **中（P3 已发生并修复）** | 高（排查耗时） | 派生属性用独有名字（`AddonGroup` / `RefAddon` / `VpkAddon` / `WorkshopAddon`）；新增 VM 属性前先查基类 |
| **R21** | **Core 后台线程触发 `PropertyChanged`** → WinUI 抛 `COMException 0x8001010E`（Avalonia 无此限制），表现为 `Unobserved task exception` | **中（P3 已发生并修复）** | 中 | `ObservableObject` 构造时捕获 `SynchronizationContext`，跨线程通知自动回投；`AddonNode.Check()` 另做点对点回投。**注意兜底对"后台线程构造的对象"无效** |
| **R22** | **WinUI 夺走视图的键盘焦点**：清空选择后焦点落到主窗口其它控件（实测「检查」按钮），视图级 `KeyDown` 再也收不到 → `Ctrl+X/V/F`、`Delete`、`F2` 全失灵 | **中（P3 已发生并修复）** | 中 | 视图 `Loaded` 时在 `XamlRoot.Content` 上 `AddHandler(KeyDownEvent, …, handledEventsToo: false)` 兜底；`handledEventsToo:false` 保证 `TextBox` 优先 |
| **R23** | **集合型派生属性用一次性快照**（`new ObservableCollection(source.Order(…))`）会吞掉异步结果（Alt 用 DynamicData 活视图） | **中（P3 已发生并修复）** | 中 | 用 `Collections/SortedObservableCollection.cs`；可见性绑 `Count` 而非集合实例 |
| **R24** | **P6 导航与布局重设计引入入口遗漏、焦点/选择丢失或生命周期回归** | 中（新增阶段，尚未实测） | 高 | 先列旧命令到新入口的映射；改动范围内验证键盘、多选、返回、切页与关闭重开；P7 再做全量回归 |
| **R25** | **设置卡片、嵌套滚动或新控件依赖导致密度/性能/兼容性下降** | 中（新增阶段，尚未实测） | 中 | 设置用卡片，数据区域保留虚拟化列表；先验证现成 API 与版本兼容，记录大目录性能和缩放/主题证据 |

---

## 5. 端到端验收清单（P7 逐项打勾）

> P6 自身的界面与交互验收见 [`P6_NATIVE_UI_DESIGN.md` §7](P6_NATIVE_UI_DESIGN.md#7-p6-验收标准)。下列为 P7 最终全量验收，包含 P3～P5 未验证项；P6 可提前验证并记录证据，但不自动视为旧遗留已销项。

- [ ] 打开目录 / 关闭目录 / 目录版本校验 / 打开 `left4dead2/addons` 被拒绝
- [ ] 自动查找游戏路径 / 手动设置 / 路径无效提示
- [ ] 导入本地 VPK / 导入 `.addonroot` / 导出 `.addonroot` / 备份文件生成
- [ ] 新建组 / 新建工坊插件 / 新建工坊合集 / 引用插件
- [ ] 启用停用（单个 / 组递归 / 启用策略冲突提示）
- [ ] 重命名（`F2`）/ 移动（`Ctrl+X`·`Ctrl+V`）/ 删除（回收站 / 保留文件）
- [ ] 搜索 / 排序（8 种）/ 标签筛选 / 扁平化显示 / 三种视图形态
- [ ] 优先级与全局优先级 / 依赖管理 / 自定义预览图
- [ ] 问题检查（全类型）/ 问题列表跳转
- [ ] VPK 冲突检查 / 冲突详情 / 忽略文件集
- [ ] 推送（软链接生成 + 清理旧链接）/ 推送失败诊断
- [ ] 启动游戏（Steam 启动项读取 / 自定义启动项 / 读取失败时可选继续）
- [ ] 工坊下载（分块并发 / 限速 / 并发数）/ 下载列表
- [ ] 替换总览（含贴图-only 识别）
- [ ] 文件清理器 / 文件名修复器 / 工坊 VPK 查找器 / 插件名自动设置器
- [ ] 设置项全部往返（保存 → 重启 → 生效）
- [ ] 单实例 / 崩溃报告 / 日志滚动 / 中英文切换
- [ ] **【R8 专项】所有命令的禁用态正确**（无目录时工具菜单灰、无选择时"对选中项操作"灰、检查中推送灰……）

---

## 6. 工作方式约定

1. **一次只推进一个 Phase**，每阶段结束提交一次可运行构建并等你验收。
2. **库里有现成能力时优先用库**（Community Toolkit / WinRT API），自写必须注明理由。
3. **严格对照 `FireAxe_Alt` 源码**核对业务行为；P6 按 [`P6_NATIVE_UI_DESIGN.md`](P6_NATIVE_UI_DESIGN.md) 重设计导航与布局，不要求复刻 Alt 界面。不做无关业务重构——此类重构留到 P7 之后单独一轮。
4. **每个新文件顶部标注来源**：`// Ported from FireAxe_Alt\FireAxe.GUI\Views\AboutWindow.axaml`。
5. 每完成一个窗口即更新 §5 勾选项与 §8 进度表。

---

## 7. 待决事项

| ID | 问题 | 结论 |
|---|---|---|
| ~~D1~~ | `FireAxe.Core` 如何处置 | ✅ **已定：B —— 拷贝进 `FireAxeWinUI3\FireAxe.Core\` 独立维护；`FireAxe_Alt` 冻结只读** |
| ~~D2~~ | 打包方式 | ✅ **已定：A —— 先非打包（zip），P7 再评估 MSIX** |
| ~~D3~~ | MVVM 框架 | ✅ **已定：B —— `CommunityToolkit.Mvvm`** |
| D4 | 观感策略 | ✅ **已定：Windows 原生界面，以 Windows 11 设置为设计参考**；P6 重设计导航、页面层级、设置卡片及全窗规范，详见 [`P6_NATIVE_UI_DESIGN.md`](P6_NATIVE_UI_DESIGN.md)。不复刻 Semi 布局 |
| ~~D5~~ | 主界面选择模型 | ✅ **已定：A —— `ListView` + `SelectedItems`** |
| ~~D6~~ | `Crash?` 调试菜单 | ✅ **已定：B —— 删除**（连同 `Operations → Crash? → Crash!!!` 整组） |
| ~~D7~~ | 是否引入 `System.Reactive` | ✅ **已定：A —— 彻底不用** |
| **D8** | **是否继续要求管理员权限**（Alt 的 manifest 是 `requireAdministrator`，主要为往 `left4dead2` 写符号链接） | 🟡 **保持 `asInvoker`**；P7 临时游戏目录下的 BCL 符号链接创建与旧链接清理测试通过，仅代表当前测试进程。已增加删除旧链接前的权限预检查与失败提示（开发者模式/手动管理员启动）；普通用户、开发者模式关闭与受管策略仍待实测，不自动提权。见 [P7 记录](phase-notes/P7-release-readiness.md) |

---

## 8. 进度追踪

| 阶段 | 状态 | 开始 | 完成 | 备注 |
|---|---|---|---|---|
| P0 Spike | ✅ **完成** | 2026-11-07 | 2026-11-08 | 首提交 `8b63c39`；结论见 `phase-notes/P0-spike.md`；S1~S5 全通过，未用退路，无残留项 |
| P1 抽象层 + VM 重写 | ✅ **完成** | 2026-11-08 | 2026-11-08 | 41 个 VM + 6 个抽象 + `FireAxe.Tests`（9 绿）；结论见 `phase-notes/P1-abstraction.md`；**残留 🟥：6 个抽象无实现、DI 未装配**（P2） |
| P2 骨架外壳 | ✅ **完成** | 2026-11-08 | 2026-11-08 | 启动/主窗口/菜单树/6 类对话框/文件夹选择/进度窗/语言切换/设置逐字节往返/关闭流程全通；结论见 `phase-notes/P2-shell.md`；**残留 🟡：输入框与文件选择框无界面证据（P3 复验）**；D8 已拍板 `asInvoker`（P5 复验） |
| P3 主界面 | ✅ **完成** | 2026-11-08 | 2026-11-08 | 12 视图 + 7 控件；真实库上浏览/三视图/多选/详情/启停/改名/移动/删除/搜索/排序/分栏/双击进组/侧键返回全通；结论见 `phase-notes/P3-main-view.md`；修掉 5 个缺陷（2 个 P1 遗漏 VM 缺陷 + Core 跨线程崩溃），R19~R23 新增；**残留 🟨：标签过滤未验生效、P2 三条对话框遗留顺延、虚拟化未复测、图标解析开销未测** |
| P4 工具窗一 | 🟡 **有条件完成** | 2026-11-09 | 2026-11-09 | P4 路由与全量 Debug 构建通过（0 警告/错误），exe 可启动；交互点验与 AddAddonTag 自动补全遗留，见 `phase-notes/P4-tool-windows.md` |
| P5 工具窗二 | 🟡 **有条件完成** | 2026-11-10 | 2026-11-10 | 三个重窗 XAML/code-behind 与 WindowService 路由落地；解释映射及冲突详情已对照 Alt；Debug 构建 0 警告/错误、全测 9/9、exe 可启动；工具窗逐项交互留待 P7，见 `phase-notes/P5-data-dense-windows.md` |
| P6 原生界面重设计 | 🟡 实施中 | 2026-11-10 | | 首轮导航、设置分类、浏览详情与工具窗已落地；构建/测试证据与未验收项见 [阶段记录](phase-notes/P6-native-ui.md) |
| P7 收尾（原 P6） | 🟡 实施中 | 2026-11-11 | | 首轮 DPI/快捷键/搜索与图片可靠性、推送预检查、ZIP/CI 落地；全量实机回归、真实图片性能与干净机器待验，见 [阶段记录](phase-notes/P7-release-readiness.md) |

---

## 9. 与 `FireAxe_Alt` 的有意差异（不是 bug，别"修"回去）

| # | 差异 | 来源 | 说明 |
|---|---|---|---|
| 1 | MVVM 框架：ReactiveUI → `CommunityToolkit.Mvvm` | D3 | ViewModel 层整体重写 |
| 2 | 观感与布局：Semi.Avalonia → Windows 原生界面（Windows 11 设置风格） | D4 / P6 | 重设计导航、页面层级、设置卡片与工具窗规范；保留业务语义，不追求旧界面复刻，详见 [`P6_NATIVE_UI_DESIGN.md`](P6_NATIVE_UI_DESIGN.md) |
| 3 | 删除 `Operations → Crash? → Crash!!!` 调试菜单 | D6 | 连带删除 `DummyCrash` 命令 |
| 4 | 打包：无 MSIX，改为 zip 分发 | D2 | P7 再评估 |
| 5 | 版本号从 **0.0.0** 起（全新项目，不承接 0.7.6） | — | 见下方注 |
| 6 | 窗口交互细节 | R3 | `WindowStartupLocation`、DPI 居中等等价实现，行为可能差几像素 |
| 7 | `IActivatableViewModel`/`WhenActivated` → **`IViewModelLifecycle`（View 显式驱动）** | P1 | VM 不再"挂到视觉树就自己开始工作"；窗口 `Loaded`/`Closed` 与列表容器复用都要调 `Activate()`/`Deactivate()` |
| 8 | 57 个 `Interaction<T,U>` 属性消失，改为 `IDialogService` / `IPickerService` / `IWindowService` | P1 | 公开成员层面就少了 57 个属性；对话框文案改由 VM 拥有 |
| 9 | `ViewModelActivator Activator` 属性消失 | P1 | ReactiveUI 类型，无法在零 Rx 的前提下保留 |
| 10 | Rx `Throttle` → `DebounceAction`；`WhenAnyValue+ToProperty` → 手写派生属性 | P1 | 去抖时长保持一致（搜索 0.5 s / 排序与标签 0.3 s） |
| 11 | 集合型派生属性**重建实例**而非保留身份 | P1 | DynamicData 原本保留元素身份；目前只在依赖/冲突/清理器/名称自动设置 4 处，若观察到选中丢失再看 |
| 12 | `AsyncRelayCommand` 默认禁止并发重入 | P1 | 见 R15 |
| 13 | 单实例：命名 Mutex → `AppLifecycle.AppInstance` + 激活重定向 | P2 | 第二实例不再只是“提示已在运行”，而是把已有实例拉到前台后退出（退出码 0） |
| 14 | Serilog 去掉 `WriteTo.Console()` | P2 | WinExe 无控制台，该 sink 写不出去；文件 sink 保留（按天滚动、留 7 份） |
| 15 | `CrashReporter` 去掉 Linux 分支 | P2 | 目标平台只有 Windows |
| 16 | 异常/长消息框正文改为**可滚动且可选中**的文本区（`ScrollViewer` + `MaxWidth 480/320`） | P2 | Alt 的 MessageBox 长文本会被裁到窗口外；这是有意改进 |
| 17 | 未移植窗口先弹“尚未移植”占位信息框 | P2 | 临时措施，P4/P5 逐个替换后删除 |
| 18 | 导入结果窗是非模态（Alt 用 `ShowDialog` 模态等待） | P2 | 行为差异小；若出现窗口叠放问题再改回模态 |
| 19 | 图标：`StreamGeometry` 资源 → **`Views/Icons.cs` 路径字符串 + `Views/Controls/IconView.cs`**（`ContentControl`，`XamlReader` 造 `Path` 直接当 `Content`） | P3 / R19 | WinUI 不能把路径写进资源（`WMC0055`），绑定的 `Geometry` 也不能给第二个元素用；代价是每个图标实例解析一次 |
| 20 | `WrapPanel` 自写（`Views/Controls/WrapPanel.cs`）；瓦片用原生 `ItemsWrapGrid` | P3 | WinUI 无内置 `WrapPanel` |
| 21 | `AddonTileSize` 的 `DynamicResource` → **`AddonTileMetrics`**（静态属性 + `TileSizeChanged` 事件） | P3 | WinUI 无 `DynamicResource`，且模板内无法 `ElementName` 回绑到 Explorer |
| 22 | 选择模型：`SelectedItems` + 代码双向同步（`SelectionMode=Extended`） | P3 / D5 | `SelectedItems` 不是依赖属性，无法绑定；`Multiple` 会画复选框 |
| 23 | 分栏：Avalonia `GridSplitter` → **自写 Border + Pointer 拖拽** | P3 | 不引入 `CommunityToolkit.WinUI.Controls.Sizers`；行为等价，clamp 200 ~ (宽度-300) |
| 24 | `SelectableTextBlock` → `TextBlock IsTextSelectionEnabled="True"` | P3 | WinUI 无该控件（`WMC0110`） |
| 25 | 派生 VM 属性改名：`AddonGroup` / `RefAddon` / `VpkAddon` / `WorkshopAddon` | P3 / R20 | `new` 遮蔽基类同名属性会让 WinUI 绑定静默失效 |
| 26 | 排序视图：DynamicData `Sort` → **`Collections/SortedObservableCollection.cs`** | P3 / R23 | 去 Rx 后的等价物：订阅源集合并保持有序 |
| 27 | Core `ObservableObject` 跨线程通知自动回投 `SynchronizationContext` | P3 / R21 | WinUI 的 `PropertyChanged` 委托禁止跨线程调用；Avalonia 无此限制 |

> 除上述 27 项外，**其余功能行为必须与 Alt 完全一致**。发现不一致时，先怀疑自己，再怀疑清单。
>
> **版本号注**：0.0.0 起手 → 移植期逐阶段递增（P3 完成记 0.1.0，P7 完成记 0.5.0，功能对齐 Alt 0.7.6 后按需继续）。
> 同时**更新检查逻辑必须指向新仓库**（`AppSettings` 里的 update repository 配置项），
> 否则会拿 0.0.0 去和旧仓库的 release 比对，导致长期误报"有新版本"。

---

*本文档随实施进展持续更新。P0 完成后应根据 `P0-spike.md` 结论修订 §3 工时与 §4 风险。*
