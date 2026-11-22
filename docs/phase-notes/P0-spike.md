# P0 · 可行性验证结论（Spike Notes）

> 阶段：P0 · 可行性验证（Spike，见 `../PORTING_PLAN.md` §2）
> 日期：2026-11-07 ~ 2026-11-08
> 环境：Windows 10.0.26340（Win11 24H2+）· .NET SDK 10.0.402 · Runtime 10.0.13
> 依赖：`Microsoft.WindowsAppSDK 2.6.5` · `CommunityToolkit.Mvvm 8.4.2` · 非打包（`WindowsPackageType=None`）
> 结论：**方案可行——S1~S5 全部通过，未使用任何退路；P1 开工前必须先吸收「对 P1 的直接影响」里的 4 条写法约束**
>
> 验证方式：`SpikeWindow` 启动后自动执行 S1 / S2 / S4 / S5，以及 S3 的构造函数、`ContentDialog`、
> 剪贴板检查，结果写入 `%LOCALAPPDATA%\FireAxe\spike.log`。
> **例外：S3 的「picker 返回真实路径」是人工点击补测**（日志中该行排在 `[done]` 之后）。
> 原始日志见文末「附：实测证据」。

---

## 结论速览

| # | 验证项 | 结论 | 对计划的影响 |
|---|---|---|---|
| S1 | `CommunityToolkit.Mvvm` 源生成器 + 绑定刷新 | ✅ **通过** | 无 |
| S2 | resx + 静态字符串在 XAML 与 C# 均可解析 | ✅ **通过（改写法）** | **省掉 +2d 的 `.resw` 迁移** |
| S3 | 非打包模式 `ContentDialog` / Picker / 剪贴板 | ✅ **通过（含返回真实路径）** | 无（R4 风险消失） |
| S4 | `ListView` 虚拟化 · 2000 项 · 三模板 | ✅ **通过** | 无（R2 风险下调） |
| S5 | `{x:Bind}` + `[ObservableProperty]` 编译期绑定 | ✅ **通过** | 无 |

**总判断：方案可行，不需要退回任何一条退路。但 P1 的翻译规则表必须按本文补充四条**
（见「对下一阶段的硬约束」），否则 44 个 VM 会集体踩坑。

---

## S1 · CommunityToolkit.Mvvm 源生成器

**通过。** 实测（`SpikeViewModel`）：

- `[ObservableProperty]` 生成属性并正确触发 `PropertyChanged`（`Name` 与派生属性 `DisplayName` 都收到）。
- `[NotifyCanExecuteChangedFor]` 让 `CanExecute` **自动**跟随属性变化：
  `Name=""` → `CanExecute=False`；`Name="beta"` → `CanExecute=True`，**无需手写** `NotifyCanExecuteChanged()`。
- `[RelayCommand]` 正常执行。

> 这条直接缓解 **R8**（`CanExecute` 漏翻译）。只要每个 `[RelayCommand]` 都配上
> `[NotifyCanExecuteChangedFor(nameof(XxxCommand))]`，灰/亮就是声明式、不会漏。

### ⚠️ 坑 1：`FireAxe.ObservableObject` 与 `CommunityToolkit.Mvvm.ComponentModel.ObservableObject` 撞名

**这是本次最重要的发现，影响全部 44 个 VM 文件。**

`FireAxe.Core\ObservableObject.cs` 定义了一个 `FireAxe.ObservableObject`。
而 ViewModel 的命名空间是 `FireAxe.ViewModels` —— **外层命名空间优先于 `using` 指令**，
所以 VM 里裸写 `: ObservableObject` 会解析到 Core 那个，源生成器随即什么都不生成。

最坑的是**它不报错**：只有下游一堆莫名其妙的
`CS0103 名称"Name"不存在` / `CS1061 未包含"Xxx"的定义`，
看不出根因是基类选错了。

**P1 解法（一次性吸收）：**

```csharp
// ViewModels\ViewModelBase.cs
namespace FireAxe.ViewModels;

// 基类写全限定名，此后所有 VM 都继承 ViewModelBase，不会再碰到这个坑
public abstract partial class ViewModelBase
    : global::CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
}
```

> 不要改 Core 里的类型名（Core 是零改动复用），也不要让每个 VM 自己写 `global::`。

### ⚠️ 坑 2：WinUI 3 下必须用 **partial property**，不能对字段用 `[ObservableProperty]`

字段写法会报 **MVVMTK0045**：

> The field `...` using `[ObservableProperty]` will generate code that is not AOT compatible
> in WinRT scenarios (such as UWP XAML and WinUI 3 apps), and a partial property should be used
> instead (as it allows the CsWinRT generators to correctly produce the necessary WinRT marshalling code)

```csharp
// ❌ 字段写法：能编译，但 WinUI 3 下报警告且 AOT 不友好
[ObservableProperty] private string _name = "initial";

// ✅ 正确写法（C# 13+ partial property）
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(DisplayName))]
[NotifyCanExecuteChangedFor(nameof(SaveCommand))]
public partial string Name { get; set; }
```

注意两点：
1. **初值不能写在属性上**（`= "initial"` 会报 CS8050），要放到构造函数里。
2. partial property **只有声明，没有实现体** —— 实现由生成器补。

### ⚠️ 坑 3：`LangVersion` 与 `CommunityToolkit.Mvvm` 版本强绑定

partial property 的实现依赖生成器输出 `field` 关键字（semi-auto property），
所以对语言版本有硬要求。实测矩阵：

| CommunityToolkit.Mvvm | `LangVersion` | 结果 |
|---|---|---|
| 8.4.0 | `latest`（C# 14） | ❌ CS9248 分布属性必须具有实现部分 |
| 8.4.0 | `13.0` | ❌ CS9248 |
| 8.4.0 | `preview` | ✅ |
| 8.4.2 | `latest`（C# 14） | ✅ |
| 8.4.2 | `13.0` | ❌ CS9248 |

**选定：`CommunityToolkit.Mvvm 8.4.2` + `<LangVersion>latest</LangVersion>`**（C# 14，`field` 已正式）。
官方博客给的替代方案是 `LangVersion=preview`（8.4.0 时代唯一可行解），
但那会引入未定型的 C# 预览特性，在 .NET 10 上没必要。

> 如果将来被迫降回 8.4.0，必须同时把 `LangVersion` 改成 `preview`，否则整个 VM 层编译不过。

---

## S2 · resx + 静态资源 — **写法必须改**

### ❌ `{x:Static}` 在 WinUI 3 里不存在

```
XamlCompiler error WMC0001: Unknown type 'Static' in XML namespace
'http://schemas.microsoft.com/winfx/2006/xaml'
```

WinUI 3 的 XAML 不支持 WPF 的 `{x:Static}` 标记扩展。计划里写的
`{x:Static r:Texts.Xxx}` 在 47 个 XAML 里一个都用不了。

### ✅ 改用 `{x:Bind r:Texts.Xxx, Mode=OneTime}` —— 零成本替换

```xml
xmlns:r="using:FireAxe.Resources"

<TextBlock Text="{x:Bind r:Texts.File, Mode=OneTime}" />
```

实测通过，XAML 里渲染出 `"文件"`，DataTemplate 里（带 `x:DataType`）同样可用。

> **这条省掉了计划里 +2 天的退路**（全量改 `{x:Uid}` + `.resw`）。
> 3,575 行的 `Texts.Designer.cs` 与两个 resx **原样可用**，只需把
> `{x:Static` 机械替换成 `{x:Bind ... , Mode=OneTime}`。

### C# 侧与卫星程序集

- `FireAxe.Resources.Texts` 命名空间、`ResourceManager("FireAxe.Resources.Texts")` 基线名
  在 WinUI 工程里**不需要改**（`RootNamespace=FireAxe` + `Resources\Texts.resx` 正好对上）。
- 卫星程序集生效：en-US `"File"` / zh-Hans `"文件"`，实测都取到。
- **zh-CN 会回退到 zh-Hans** —— 本机 `CurrentUICulture=zh-CN` 时直接拿到 `"文件"`
  （.NET 的 `zh-CN → zh-Hans → zh` 父链）。这意味着 `Texts.zh-Hans.resx` 一个文件即可覆盖简中。

### 关于 R7（本地化热切换）

实测确认：XAML 侧文本在窗口创建时求值，切换 `CurrentUICulture` 后**不重建窗口就不会更新**。
与计划中「切换后重启窗口（可接受）」一致。

---

## S3 · 非打包模式下的对话框 / 选择器 / 剪贴板

全部通过，且**无需任何额外 hack**。

| 能力 | 实测结果 |
|---|---|
| `ContentDialog` | ✅ 弹出成功（`XamlRoot = Content.XamlRoot`），`ShowAsync` 返回正常 |
| `Microsoft.Windows.Storage.Pickers.FileOpenPicker(AppWindow.Id)` | ✅ **弹出并返回真实路径**：`<WORKSPACE_ROOT>\PORTING_PLAN.md` |
| `Microsoft.Windows.Storage.Pickers.FolderPicker(AppWindow.Id)` | ✅ 构造成功（与上面同族 API） |
| 传统 `Windows.Storage.Pickers` + `InitializeWithWindow(hwnd)` | ✅ 初始化成功（`hwnd=0x80C64`） |
| 剪贴板往返 `SetContent` → `GetTextAsync` | ✅ `"fireaxe-spike"` |

**重要：WinAppSDK 2.6.5 已经带新版 Picker**（`Microsoft.Windows.Storage.Pickers`），
构造时直接吃 `AppWindow.Id`，**不需要 `WindowNative.GetWindowHandle` + `InitializeWithWindow`**。

> **R4（Picker 需 HWND 初始化）风险消失。**
> P1 写 `IPickerService` 时用新 API；传统路径留作退路。

---

## S4 · `ListView` 虚拟化（2000 项 · 三种 `ItemTemplate`）

**通过。**

```
items=2000, realized containers=50      ← 虚拟化生效（50 ≪ 2000）
30 × ScrollIntoView = 74 ms             ← 平均 2.5 ms/次
```

- 用 `DataTemplateSelector` 在三套 `ItemTemplate`（网格行 / 瓦片 / 紧凑）之间按索引切换，
  虚拟化照常工作，容器数稳定在视口量级。
- `ScrollIntoView` 跳转没有明显卡顿。

> **R2 风险下调**：`ListView` + `ItemsStackPanel` + 三模板的方案可用，
> 不需要退回「砍瓦片视图」。
> （严格说这是 2000 项 × 三种模板的滚动路径，真实插件库的图片解码负载更大，
> 到 P3 仍要按真实库复测一次。）

---

## S5 · `{x:Bind}` + `[ObservableProperty]`

**通过。** 全部走编译期绑定，无 `{Binding}` 回退需要。

| 绑定目标 | 结果 |
|---|---|
| 普通属性 `Name`（`Mode=OneWay`） | ✅ 更新为 `"gamma"` |
| 派生属性 `DisplayName` | ✅ 更新为 `"[gamma]"`（靠 `[NotifyPropertyChangedFor]`） |
| `int` 属性 `Counter` | ✅ 显示 `"42"` |
| 命令 `SaveCommand` | ✅ `Command` 引用与 VM 一致 |

> 派生属性不会自动通知 —— 必须显式挂 `[NotifyPropertyChangedFor(nameof(X))]`。
> 这对应计划里 `WhenAnyValue(...).Select(...).ToProperty(...)` 的 124 + 25 处翻译。

### ⚠️ 坑 4：供 XAML 使用的数据类不能有 `init` 属性

`SpikeItem` 最初用了 `{ get; init; }`，生成的 `XamlTypeInfo.g.cs` 直接编译失败：

```
XamlTypeInfo.g.cs(842,13): error CS8852: 只能在对象初始值设定项中或在实例构造函数或
"init" 访问器中的 "this" 或 "base" 上分配 init-only 属性或索引器 "SpikeItem.Title"
```

原因是 XAML 类型信息生成器会为每个属性生成 setter。凡是出现在
`x:DataType` / `ItemTemplate` 里的类型，属性一律用普通 `get; set;`。

---

## 其他踩坑（不在验证项里，但会浪费工时）

| 问题 | 现象 | 解法 |
|---|---|---|
| `Window` 没有 `Resources` 属性 | `<Window.Resources>` 无法使用 | WinUI 3 的 `Window` 不是 `FrameworkElement`；资源挂到根内容元素（如 `<Grid.Resources>`） |
| `UIElement` 没有 `Loaded` 事件 | `Content.Loaded` 编译不过 | `((FrameworkElement)Content).Loaded += ...` |
| `DataTemplate` / `DataTemplateSelector` 命名空间 | `using Microsoft.UI.Xaml.Data` 找不到 | `DataTemplate` 在 `Microsoft.UI.Xaml`，`DataTemplateSelector` 在 `Microsoft.UI.Xaml.Controls` |
| `maxversiontested` 放错位置 | 启动报 **"应用程序的并行配置不正确"**（SxS） | 必须放 `<compatibility><application>` 下，不能放 `<application><windowsSettings>` |
| `dotnet new sln` 默认产出 `.slnx` | 与计划的 `FireAxe.sln` 不符 | `dotnet new sln --format sln` |
| 非打包应用需 Windows App Runtime | 本机已装 2.6.5，`Packaged=False` 正常启动 | 分发时必须带前提说明或改 `WindowsAppSDKSelfContained=true` |

---

## 风险联动

| 风险号 | 变化 | 理由 |
|---|---|---|
| **R2**（`ListView` 虚拟化多选+三模板性能） | ↓ 上调为 **低-中** | 实测 2000 项只实体化 50 个容器、30 次 `ScrollIntoView` 43~74 ms，方案可用；P3 需按真实插件库（含图片解码负载）复测 |
| **R4**（非打包模式 Picker/剪贴板需 HWND） | ↓ 上调为 **低** | WinAppSDK 2.6.5 自带 `Microsoft.Windows.Storage.Pickers`，构造吃 `AppWindow.Id`，**不需要 HWND**；剪贴板往返正常 |
| **R8**（`CanExecute` 可观察性漏翻译） | ↓ 上调为 **中** | `[NotifyCanExecuteChangedFor(nameof(XxxCommand))]` 声明式生效，无需手写 `NotifyCanExecuteChanged()` |
| **R11**（`FireAxe.ObservableObject` 与 CommunityToolkit 撞名 → 源生成器静默失效） | ✚ **新增（高，已发生）** | 外层命名空间优先于 `using`，VM 裸写 `: ObservableObject` 会解析到 Core 那个，生成器什么都不生成且不报错。由 `ViewModelBase` 一次性吸收 |
| S2 退路「`{x:Uid}` + `.resw`（+2d）」 | ✖ **消失** | `{x:Bind r:Texts.Xxx, Mode=OneTime}` 零成本替换，`Texts.Designer.cs` 与两个 resx 原样可用。+2d 本属退路预算、不在基线内，故总工时 19.5~28 人日不变 |

## 对下一阶段的硬约束（P1 · 先改这四条再动手）

把下面四行补进 `../PORTING_PLAN.md` §3 P1.1 的翻译规则表：

| ReactiveUI | CommunityToolkit.Mvvm | 补充的坑（P0 实测） |
|---|---|---|
| `ReactiveObject` / `ViewModelBase` | `ObservableObject` | **基类必须全限定或经 `ViewModelBase` 继承**，否则撞 `FireAxe.ObservableObject`（静默失败） |
| 可观察属性 | `[ObservableProperty] public partial T X { get; set; }` | **只能用 partial property**（字段写法触发 MVVMTK0045）；初值放构造函数 |
| `WhenAnyValue(...).ToProperty(...)` | `[ObservableProperty]` + `[NotifyPropertyChangedFor]` | 派生属性**必须显式**挂 `NotifyPropertyChangedFor` |
| `{x:Static r:Texts.Xxx}`（XAML） | `{x:Bind r:Texts.Xxx, Mode=OneTime}` | WinUI 3 无 `x:Static`；机械替换即可 |

另外两条：

1. **数据模型类不要用 `init` 属性**（会炸 `XamlTypeInfo.g.cs`）。
2. **`Services` 层的 Picker 优先用 `Microsoft.Windows.Storage.Pickers`**（吃 `AppWindow.Id`，免 HWND）。
3. **先建 `ViewModelBase` 再写第一个 VM**（见遗留项 🟥）。

## 对计划的修订建议

- §3 P1 的 `LangVersion`：明确写 `<LangVersion>latest</LangVersion>` +
  `CommunityToolkit.Mvvm 8.4.2`（8.4.0 不可用）。
- §2 目录结构里的 `.github/workflows/`：本阶段补了简版 build workflow，
  **尚未经过首次 push 验证**，见下方遗留项。
- §4 风险登记册按上表同步（R2/R4/R8 下调、新增 R11、S2 退路作废）。

---

## 遗留项（分级）

| 项 | 级别 | 归属阶段 | 备注 |
|---|---|---|---|
| 建 `ViewModelBase`（吸收 R11） | 🟥 **阻断** | P1 第一步 | 在它存在之前，任何新 VM 都会再踩一次撞名静默失败 |
| 删除 Spike 痕迹（`SpikeWindow` / `SpikeViewModel` / `SpikeTemplateSelector`，`App.xaml.cs` 改回主窗口） | 🟨 可延后 | P1 第一步 | 清单见下 |
| `.github/workflows/build.yml` 首次 push 验证 | 🟨 可延后 | P1 | 本阶段补的简版 CI，本地无法验证 Actions 行为 |
| S4 虚拟化在真实插件库（含图片解码负载）下的性能复测 | ⬜ 观察 | P3 | 本次只测了纯文本 2000 项 |
| R7 本地化热切换需重建窗口的体感 | ⬜ 观察 | P4 | 计划已按"切换后重启窗口"设计 |

### P1 动手前要删的 Spike 文件

- `FireAxe.WinUI3\SpikeWindow.xaml` / `.xaml.cs`
- `FireAxe.WinUI3\ViewModels\SpikeViewModel.cs`
- `FireAxe.WinUI3\Converters\SpikeTemplateSelector.cs`（`SpikeItem` 一并删）
- `App.xaml.cs` 里 `new SpikeWindow()` 改回主窗口

---

## 附：实测证据

> 以下为一次实际运行的原样日志，数值随机器与负载浮动（另一次运行是 30 次 `ScrollIntoView` = 74 ms）。

```
=== FireAxe WinUI 3 P0 spike · 2026-11-08T00:15:18.2468283+08:00 ===
[env] OS      = Microsoft Windows NT 10.0.26340.0
[env] Runtime = 10.0.13
[env] Packaged= False
[env] UICulture = zh-CN
[S1] PropertyChanged on Name=True, on derived DisplayName=True (events: Name,DisplayName)
[S1] CanExecute auto-refresh: empty→False (expect False), set→True (expect True)
[S1] RelayCommand executed, count=1 (expect 1)
[S2] Texts.File en-US="File" zh-Hans="文件" fallback="文件"
[S2] satellite ok = True
[S2] XAML x:Bind static -> "文件"
[S5] x:Bind Name     -> "gamma" (expect "gamma")
[S5] x:Bind derived  -> "[gamma]" (expect "[gamma]")
[S5] x:Bind int      -> "42" (expect "42")
[S5] x:Bind command wired -> True (expect True)
[S3] ContentDialog shown=True (expect True), auto-close result=None
[S3] new Pickers.{FileOpenPicker,FolderPicker}(AppWindow.Id) ctor ok
[S3] legacy FileOpenPicker + InitializeWithWindow(hwnd=0x80C64) ok
[S3] clipboard roundtrip -> "fireaxe-spike" (expect "fireaxe-spike")
[S4] items=2000, realized containers=50 (虚拟化生效则远小于总数)
[S4] 30 × ScrollIntoView = 43 ms
[done] 自动化检查结束（S3 需要人工点击按钮）
[S3] new Pickers.FileOpenPicker -> <WORKSPACE_ROOT>\PORTING_PLAN.md
```

最后一行是人工点选 `PORTING_PLAN.md` 后由 `OnOpenFileClick` 追加的——它证明非打包模式下
新 Picker 确实能返回真实路径，而不是只"构造成功"。
