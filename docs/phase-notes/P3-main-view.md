# P3 · 主界面（Explorer + 详情面板）

> 阶段：P3 · 主界面（见 `PORTING_PLAN.md` §2 / §3）
> 日期：2026-11-08 ~ 2026-11-08
> 环境：Windows 10.0.26100 · .NET SDK 10.0 · WinAppSDK **2.6.5**（非打包）· CommunityToolkit.Mvvm 8.4.2 · Serilog 4.3.0 · 实测 DPI 150%（`windowDpi=144`）
> 结论：**可继续（P4 可开工）** —— 主界面 12 个视图 + 7 个自定义控件全部落地，真实插件库上"浏览 / 三视图 / 多选 / 详情 / 启停 / 改名 / 移动 / 删除 / 搜索 / 排序 / 分栏拖拽 / 双击进组 / 侧键返回"实机跑通。
> 本阶段修掉 **5 个真实缺陷**（其中 2 个是 P1 遗留的 VM 缺陷，1 个是 Core 级 WinRT 跨线程崩溃），并**推翻了 3 个 P0/P2 时期的写法假设**（见 §4 新增风险）。

---

## 2. 结论速览

| # | 验证项 / 交付项 | 结论 | 对计划的影响 |
|---|---|---|---|
| V1 | `AddonNodeExplorerView` 三视图形态（表格 / 瓦片 / 小瓦片 / 大瓦片） | ✅ 通过 | 一套 `ListView` + 两个并列 `ListView` + `ItemsWrapGrid`；`WrapPanel` 自写 |
| V2 | 瓦片尺寸随 `TileViewSize` 变化 | ✅ 通过 | 修掉 P1 占位常量 200；真值 75/100/150 |
| V3 | 选择模型（单选 / 多选 / View↔VM 双向） | ✅ 通过 | `SelectionMode=Extended` + 代码同步，`SelectedItems` 不可绑 |
| V4 | 详情面板（组 / 引用 / VPK / 工坊 VPK 四种 Section） | ✅ 通过 | 字段级对齐 Alt；工坊字段 8 项全部渲染 |
| V5 | 内联改名（F2 / 铅笔按钮 / Enter·Esc / 失败回滚） | ✅ 通过 | `EditableTextBlock` 自定义控件成立 |
| V6 | 键盘：`Ctrl+X`·`Ctrl+V`·`Ctrl+F`·`Delete`·`F2` | 🟡 有条件通过 | **必须挂窗口级 fallback**（见 §3.6，R22）；Explorer 自己会丢焦点 |
| V7 | 鼠标侧键（`XButton1`）返回父组 | 🟡 有条件通过 | 只在空白区触发；列表项上被 `ListView` 消费（Alt 同样行为，见 §3.7） |
| V8 | 双击组进入（含引用指向组） | ✅ 通过 | 用 `FindAncestor<ListViewItem>` 反查，WinUI 无 `ContainerFromElement` |
| V9 | 右键上下文菜单（8 组 19 项） | ✅ 通过 | `Border.ContextFlyout` + 代码事件（flyout 不在视觉树，命令绑定不可靠） |
| V10 | 新建组 / 删除（确认框 + 磁盘删除） | ✅ 通过 | `ContentDialog` 走通 P2 的 `IDialogService` |
| V11 | 移动（`Ctrl+X` → 移动条 → 进组 `Ctrl+V` / 勾选按钮） | ✅ 通过 | 磁盘往返验证 |
| V12 | 搜索（0.5 s 去抖、实时、排序分支下也生效） | ✅ 通过 | 修掉 2 个 P1 缺陷 + 1 个绑定缺陷（§3.9） |
| V13 | 排序 / 升降序 / 视图切换 | ✅ 通过 | 名称排序顺序与手算一致 |
| V14 | 分栏拖拽 | ✅ 通过 | 自写 Pointer 拖拽（未引入 CommunityToolkit.Sizers）；clamp 生效 |
| V15 | 图标机制 | ⛔ 原方案否决 → ✅ 换路通过 | `PathIcon.Data` 绑定**运行时必崩**（R19）；改 `Icons.cs` 字符串 + `IconView` |
| V16 | 派生 VM 属性绑定 | ⛔ 原写法否决 → ✅ 换路通过 | `new` 遮蔽基类属性会让整条 `{Binding}` 链**静默失效**（R20） |
| V17 | Core 后台线程 `PropertyChanged` | ⛔ 原写法否决 → ✅ 换路通过 | WinUI 抛 `COMException 0x8001010E`（R21），Core 已加回投 |
| V18 | 标签过滤面板（`AddonTagCheckBoxList`） | 🟡 有条件通过 | 只验证到"渲染 + All/None 按钮在"，**未取得筛选生效证据**（真实库根目录无标签） |
| V19 | 虚拟化性能（真实库大目录） | 🟡 有条件通过 | 未复测：真实库只有单根组，无法造 2000 项场景；R2 维持"低-中" |

**总判断：** P3 通过标准（"日常可用"）在真实插件库上达成。P4 开工前必读 §5——尤其是第 1 条（图标）、第 2 条（派生属性命名）、第 4 条（Core 回投）与第 7 条（焦点 fallback），
这四条都是"不照做就**静默失效或启动即崩**"的类型，靠肉眼审查看不出来。

---

## 3. 逐项详述

### 3.1 落地清单（对照 §3 计划表）

| 视图 / 控件 | XAML | code-behind | 状态 |
|---|---:|---:|---|
| `AddonNodeExplorerView` | 495 | 594 | 新建 |
| `AddonNodeView` | 337 | 183 | 新建 |
| `EditableTextBlock` | 79 | 320 | 新建（自定义控件） |
| `WorkshopVpkAddonSectionView` | 242 | 47 | 新建 |
| `AddonNodeGridRowView` | 91 | 44 | 新建 |
| `AddonNodeTileView` | 59 | 71 | 新建 |
| `AddonGroupSectionView` / `VpkAddonSectionView` / `VpkAddonConflictingDetailsView` / `RefAddonNodeSectionView` / `AddonNodeDependenciesView` / `DownloadItemView` / `AddonNodeNavBarView` / `AddonNodeNavBarItemView` / `AddonNodeSectionViewDecorator` | 小 | 小 | 新建 |
| `AddonIconView` / `AddonNodeEnableButton` / `AddonRefView` / `AddonTagView` / `AddonTagCheckBoxList` | 小 | 小 | 新建 |
| 基础设施：`Views/Icons.cs`(96) / `Views/Converters.cs`(339) / `Views/EnumSources.cs`(54) / `Views/Theme/Styles.xaml`(88) / `Views/Controls/IconView.cs`(143) / `Views/Controls/WrapPanel.cs`(153) / `Views/Controls/AddonTileMetrics.cs`(33) / `Collections/SortedObservableCollection.cs`(132) | | | 新建 |
| `Views/MainWindow.xaml` | | | 改：占位 → 真实 Explorer（**P2 硬约束 #8 结清**） |
| `Views/XBind.cs` | | | 改：+14 个 x:Bind 辅助函数 |
| `ViewModels/*`（4 个派生 VM）/ `ViewModels/AddonNodeExplorerViewModel.cs` | | | 改：属性改名 / 2 个缺陷修复 / 活排序集合 |
| `FireAxe.Core/ObservableObject.cs`、`AddonNode.cs` | | | 改：跨线程回投（R21） |
| `Services/IWindowService.cs`、`WindowService.cs` | | | 改：+3 个占位入口（标签编辑器 / 自定义图片 / 节点选择器），P4/P5 逐个替换 |

构建：`dotnet build FireAxe.sln -c Debug` → **0 警告 0 错误**；产物 `FireAxe.WinUI3\bin\x64\Debug\net10.0-windows10.0.26100.0\FireAxe.exe`。

### 3.2 `{x:Bind}` 在详情面板里绑不上名称（R20，⛔→✅）

**怎么发现的。** 详情面板在真实库上只有 4 条文本（`启用/禁用`、`路径：`、`优先级`、`0`），名称、标签、依赖项、文件大小、高级面板全都不出现；同一时刻列表瓦片上的名称是正常的。

**怎么定位的。** 在 `EditableTextBlock` 的 `OnValueOrDisplayChanged` 打日志：选中节点后只有 `priorityEditingControl` 收到 `new='0'`，`nameEditingControl` 一次都没有。
再用探针转换器（`{Binding X, Converter=Probe}`，转换器里记 `param/value`）逐段试：

| 绑定表达式 | 是否触发转换器 |
|---|---|
| `{Binding Addon.Name}` | 否 |
| `{Binding Addon}` | 否 |
| `{Binding EnableState}`（基类属性） | 是（`AddonNodeEnableState.Disabled`） |
| `{Binding AddonId}`（基类 Guid） | 是（`55d78468-…`） |

→ 不是"继承属性不可绑"，而是 **`Addon` 这个属性名本身解析失败**。

**根因。** 4 个派生 VM 用 C# `new` 遮蔽了基类同名属性：

```csharp
public new AddonGroup? Addon => (AddonGroup?)base.Addon;      // AddonGroupViewModel
public new RefAddonNode? Addon => …                            // RefAddonNodeViewModel
public new VpkAddon? Addon => …                                // VpkAddonViewModel
public new WorkshopVpkAddon? Addon => …                        // WorkshopVpkAddonViewModel
```

基类是 `AddonNodeSimpleViewModel.Addon`（`AddonNode?`）。同名不同型 → WinUI 3 的反射式 `{Binding}` 路径解析**静默失败**（无异常、无日志），整条 `Addon.*` 下游全废；Avalonia 的绑定对此无感，所以 Alt 原版没问题。

**验证。** 把 `AddonGroupViewModel.Addon` 那行注释掉重建 → 详情面板立刻从 4 条文本变成 18 条（出现名称、全局优先级、标签、依赖项、创建时间）。

**改法。** 4 个派生属性改成独有名字并同步调用点：`AddonGroup` / `RefAddon` / `VpkAddon` / `WorkshopAddon`；改到 4 个 XAML 绑定点（`AddonGroupSectionView.xaml` 的 `EnableStrategy`、`RefAddonNodeSectionView.xaml` 的两个 `IsChecked`、`WorkshopVpkAddonSectionView.xaml` 的 `IsAutoUpdate`）与 `WorkshopVpkAddonViewModel` / `VpkAddonViewModel` / `RefAddonNodeSectionView.xaml.cs` 的 C# 站点。

**坑。** 注释式临时验证会留下 CS1061；`new` 遮蔽的语义差异在编译期完全合法，**只有运行时绑定才失败**。

### 3.3 `PathIcon.Data` 不能绑定，重几何也不能共享（R19，⛔→✅）

**怎么发现的。** P3 首版用 `<PathIcon Data="{x:Bind local:Icons.Search, Mode=OneTime}"/>`（`Icons` 暴露缓存的 `Lazy<Geometry>`），**启动即崩**：

```
[FTL] Unhandled Exception
System.ArgumentException: Value does not fall within the expected range.
  at ABI.Microsoft.UI.Xaml.Controls.IPathIconMethods.set_Data(IObjectReference _obj, Geometry value)
  at FireAxe.Views.AddonNodeExplorerView.XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_PathIcon_Data(…)
  at Update_FireAxe_Views_Icons_Search(Geometry obj, Int32 phase)
  at AddonNodeExplorerView_obj1_Bindings.Initialize()
```

**关键实验。** 在 `Icons.Parse` 里用 `XamlReader.Load("<PathIcon Data=\"…\"/>")` 造出图标，取出它的 `Geometry` 再往别处赋值：

```
Probe PathIcon: FAILED        // 另一个 PathIcon.Data = 该 Geometry
Probe Path: FAILED            // Shapes.Path.Data = 该 Geometry
Probe GeometryGroup: FAILED   // GeometryGroup.Children.Add → COMException 0x800F1000
Probe InlinePathIcon: OK      // 直接用 XamlReader 造出来的那个 PathIcon 实例
```

→ **XamlReader 解析出的 `Geometry` 属于创建它的元素，不能迁移给第二个元素**；"缓存几何 + 分发给多个图标"的方案根本不存在。

**改法（最终形态）。**

1. `FireAxe.WinUI3\Views\Icons.cs`：`public static class Icons`，只暴露 **16 个 `string`**（路径数据逐字抄 `FireAxe_Alt\FireAxe.GUI\Assets\Icons.axaml` 的 `StreamGeometry`）。
2. `FireAxe.WinUI3\Views\Controls\IconView.cs`：`ContentControl`，DP `Data`(string) / `Stroke` / `StrokeThickness`；`UpdateContent()` 用
   `XamlReader.Load("<Path xmlns='…' Data='…' Stretch='Uniform'/>")` 造 `Path` 并**直接当 Content**；`ApplySize()` 手动把 `path.Width/Height = ActualWidth/ActualHeight`（`Path + Stretch=Uniform` 会撑成几何自身尺寸）；`ApplyFill()` 把 `Foreground` 传给 `path.Fill`。
3. 全部调用点 `<PathIcon …>` → `<ctl:IconView …>`（10 个 XAML 文件）。
4. `AddonNodeEnableButton` 内部 `Geometry data` → `string data`。

**代价。** 每个图标实例解析一次 XAML 字符串；列表项多时有可观开销（未单独测，见 §6 🟨）。

**坑。** WinUI 3 **不能**把路径数据写成资源：`<PathGeometry Figures="{StaticResource …}">` 报 `WMC0055`（`Figures` 是 `PathFigureCollection`，没有字符串转换器）。`PathIcon.Data` 内联写 `M…` 字符串是**可以的**（XAML 编译器有转换器），`PathGeometry.Figures` 不行。

### 3.4 图标加载与图片解码（`AddonIconView` / `IImageLoader`）

**✅ 通过。** `AddonIconView` 保留 Alt 的三态（图片 / 问号 / 文件夹）与 `<ctl:IconView>` 缩放；`ImageDecodingWidth`、`SpecialIconSize` 与 `AddonTileMetrics` 联动（瓦片尺寸变化时同时设 `ImageSize` 与 `SpecialIconSize`）。
证据：三档瓦片外框实测（150% DPI，见 §7）与几何换算完全吻合，说明尺寸链路真实生效。

### 3.5 `EditableTextBlock`（内联改名）

**✅ 通过。** 模板拆 `PART_DisplayView`（`TextBlock` + 铅笔按钮）/ `PART_EditView`（`TextBox` + 提交 + 取消）；
`StartEditing()` 复制值、聚焦、全选；`Submit()` 先 raise `Submitting`，宿主 try/catch 写 VM（`AddonNode.Name` 的 setter 会抛），失败回滚并保持编辑态；`Enter`（非 Shift）提交、`Esc` 取消、`DataContext` 变化即退出编辑。

**实测证据。** 选中 `Time to Die (Campaign)` → `F2` → `PART_TextBox` 出现且带原值并获焦 → UIA `ValuePattern.SetValue("P3renamed")` + `Enter` →
列表瓦片、详情面板名称、**磁盘目录名**三处同时变成 `P3renamed`，编辑框关闭（见 §7）。

**差异。** WinUI 无 `SelectableTextBlock`（`WMC0110`），显示态用 `<TextBlock IsTextSelectionEnabled="True"/>`。

### 3.6 键盘路由：焦点会逃出 Explorer（R22，✅ 有条件通过）

**现象。** `Ctrl+X`/`Ctrl+V`/`Ctrl+F`/`Delete`/`F2` 在"刚进组 / 刚清空选择"之后**全部失灵**。

**根因（两条）。**

1. `KeyDown` 挂在 `AddonNodeExplorerView` 这个 `UserControl` 上，只处理**冒泡到它**的按键。双击进组会把选择清空 → 被聚焦的 `ListViewItem` 消失 → WinUI 把焦点交给窗口里第一个可聚焦控件（实测是主窗口状态栏的「检查」按钮，**在 Explorer 之外**）→ 按键再也到不了 Explorer。
2. 焦点在搜索框时，`TextBox` 自己消费 `Ctrl+X`/`Ctrl+V`（正确行为，不能抢）。

**改法。**

- `CurrentGroup` 变化时 `FocusCurrentList()`（瓦片可见就聚焦瓦片列表，否则表格列表），并 `DispatcherQueue.TryEnqueue(Low, FocusCurrentList)` 再抢一次。
- 窗口级兜底：`Loaded` 里 `XamlRoot.Content.AddHandler(KeyDownEvent, OnKeyFallbackKeyDown, handledEventsToo: false)`，`Unloaded` 移除；`OnKeyFallbackKeyDown` 先判 `e.Handled`，没被处理才转给 `OnKeyDown`。**`handledEventsToo: false` 是刻意的**——`TextBox` 标记为已处理的键不再进入我们的逻辑。

**实测证据。** 进组后按 `Ctrl+F` → UIA 焦点为 `ControlType.Edit name='搜索'`；`Ctrl+X` → 移动条出现；`XButton1` 返回根 → `Ctrl+V` → 磁盘上目录回到根（§7）。

### 3.7 双击进组 / 侧键返回 / 上下文菜单

**✅ 双击进组。** WinUI 3 没有 `ItemsControl.ContainerFromElement`（`CS0117`），改用自写 `FindAncestor<ListViewItem>(VisualTreeHelper.GetParent 循环)` 取 `item.Content`，
判 `AddonGroup` 或 `RefAddonNode { ActualSourceAddon: AddonGroup }` → `GotoGroup` + `e.Handled = true`。真实库：`260928` → 11 个子组 → `地图` → 19 个真实 addon。

**🟡 侧键返回。** `PointerReleased` 读 `e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.XButton1Released`。
`mouse_event(0x0080/0x0100, dwData=1)` 在**空白区**按下 → 返回父组成功；在**列表项上方**按下时事件被 `ListView` 消费，我们的 handler（XAML 属性挂的）收不到。
与 Alt 的差异仅在"挂载层级"，行为等价（Alt 同样依赖指针事件冒泡），故列为有条件的通过。

**✅ 上下文菜单。** `Border.ContextFlyout` + `MenuFlyout`，19 项全部用 `Click` 处理器（flyout 不在视觉树里，`{Binding}` 到 VM 命令不可靠）；`bool` 参数用 `XBind.True/False`。
实测渲染：刷新 / 新建▸（组·引用模组·创意工坊模组·创意工坊合集）/ 根据选中创建引用模组 / 启用 / 禁用 / 递归启用 / 递归禁用 / 移动 / 移动到这里 / 取消移动 / 删除 / 删除并保留文件 / 更多▸。
**坑：`MenuFlyoutSubItem` 必须点击展开，hover 不展开**（自动化时容易误判"子菜单没渲染"）。

### 3.8 分隔条（自写拖拽）

**✅ 通过。** 未引入 `CommunityToolkit.WinUI.Controls.Sizers`（csproj 保持不引包）；`Border` 4px + `PointerPressed/CapturePointer`，移动量在 UserControl 级 `PointerMoved` 里算，
`Math.Clamp(width, 200, max(200, rootGrid.ActualWidth - 300))`。实测拖到上限 **465.33**（`rootW=765.33`），详情列变宽、搜索框保留。

**同时修掉的问题。** 拖宽详情列后搜索框被挤没（行 1 的两列本来是 `*` + `Auto`）→ 改为 `Auto` + `*`，让搜索框保住 `MinWidth=150`，被裁的是尾部按钮/下拉。

**验证方法上的坑。** `SetCursorPos` 不产生 `WM_POINTER` 更新，MCP 的 `Move(drag=true)` 同样不行 → 拖拽类交互必须用 `mouse_event(MOUSEEVENTF_MOVE, dx, dy)` 发**真实相对移动**才驱动 WinUI 的 `PointerMoved`。这不是应用缺陷。

### 3.9 搜索：三处缺陷（P1 遗留 2 处 + 绑定 1 处）

日志证据（修复前，探针前缀 `[P3probe]`）：

```
21:49:41.842 Search start input=11 text='HUD' includeName=true ignoreCase=true flatten=false regex=false tagsNull=true
21:49:41.852 SearchAsync finished, results=1     ← 匹配到 1 条
```

界面却显示"空"。三个原因：

1. **排序分支把异步结果丢掉（P1 遗留，A 类）**：`RefreshCurrentNodes()` 里
   `new ObservableCollection<AddonNode>(rawNodes.Order(...))` 是**一次性快照**，而 `SearchAsync` 的结果是稍后异步 Post 进 `rawNodes` 的。
   Alt 用的是 DynamicData `rawNodes.ToObservableChangeSet().Sort(comparer).Bind(out nodes)`——**持续订阅源集合**的活视图。
   → 新增 `Collections/SortedObservableCollection.cs`（订阅源 `INotifyCollectionChanged`，Add 走二分插入并 `Insert` 发通知、Remove 转发、Reset/Move/Replace 走 `Rebuild()`，`Dispose()` 退订），排序分支改用它。
2. **`TextBox` 双向绑定只在失焦时回写**：`Text="{Binding SearchText, Mode=TwoWay}"` → 打字期间 VM 收不到，Alt 是边打边搜（0.5 s 去抖）→ 加 `UpdateSourceTrigger=PropertyChanged`。
3. **空提示不会消失**：`Visibility="{Binding CurrentNodes, Converter=EmptyEnumerableToVisibility}"` 只在集合**实例**被替换时求值；活集合异步填充不触发 PropertyChanged → 改绑 `CurrentNodes.Count` + 新增 `ZeroToVisibilityConverter`。

**修复后证据。** 进组 `260928` → 输入 `HUD` → 列表只剩 `HUD&击杀反馈`；清空后 11 项恢复。

**排查中被排除的伪因（记下来防止下次重踩）。**

- 中文输入法：`SendKeys`/`Type` 打进搜索框只产生**组合态**文本（截图可见候选窗），`TextBox.Text` 未提交 → VM 收不到。这是"输入了但没日志"的首因。
- 窗口在后台：按键落到别的窗口。
- 匹配逻辑本身（`DefaultStringMatcher`、`SearchNode.Match`、`AddonNodeSearchOptions` 默认值）**无缺陷**，不要再去改它。

### 3.10 另一处 P1 遗留 VM 缺陷：`CurrentGroup` 不刷新列表（A 类）

`AddonNodeExplorerViewModel.CurrentGroup` 的 setter 只做了 `ObserveCurrentGroup` + NavBar + 清空选择，**不刷新列表**；
Alt 用 `WhenAnyValue(CurrentGroup, SortMethod, IsAscendingOrder, SearchOptions).Subscribe(_ => RefreshCurrentNodes())` 统一刷新，P1 移植时把后三个各自接了刷新却漏了 `CurrentGroup`。
症状：双击进组后导航栏更新、列表仍是上一级内容。→ setter 末尾补 `RefreshCurrentNodes()`。
证据：修复后双击首项，列表从 1 项变 11 项，导航栏出现 `260928`。

### 3.11 Core 级 WinRT 跨线程崩溃（R21，⛔→✅）

**现象。** 日志出现 `[ERR] Unobserved task exception occurred`：

```
System.Runtime.InteropServices.COMException (0x8001010E)
  at ABI.System.ComponentModel.PropertyChangedEventHandler.NativeDelegateWrapper.Invoke(…)
  at FireAxe.ObservableObject.NotifyChanged(String propertyName)          ← ObservableObject.cs:29
  at FireAxe.AddonNode.set_CheckTask(Task value)                          ← AddonNode.cs:194
  at FireAxe.AddonNode.<>c__DisplayClass166_0.<Check>b__1(Task _)         ← AddonNode.cs:1247
```

**根因。** `AddonNode.Check()` 里 `checkTask.ContinueWith(...)` 在**线程池线程**把 `CheckTask` 置 `null` → `PropertyChanged` 从非 UI 线程发出。
WinUI 的绑定订阅的是 **WinRT 委托**，跨线程调用直接抛 `RPC_E_WRONG_THREAD`；Avalonia 的委托不校验，所以 Alt 一直没暴露。
（`{Binding Addon.CheckTask}` 是第一个真正订阅 `AddonNode.PropertyChanged` 的 WinUI 消费者，所以 P3 才炸。）

**改法（两层）。**

1. 点对点：`AddonNode.Check()` 捕获 `SynchronizationContext.Current`（即调用方 UI 线程），continuation 内用 `synchronizationContext.Post` 执行"清 `CheckTask` + 记录 `checkTask.Exception`"，为 `null` 时保持原路径。
2. 系统性：`ObservableObject` 在构造时捕获 `SynchronizationContext`，`NotifyChanged` 若发现当前线程上下文与捕获的不一致就 `Post` 回投（用 `(this, args)` 元组 + 静态 lambda，不产生闭包）；捕获为 `null`（对象在后台线程构造）时保持旧行为。

**验证。** 修复前日志有 1 次 `8001010E`（23:06:44）；修复后重启加载真实库（会触发成批 VPK 检查），新增 20 行日志里 `8001010E` **计数 0**，只有真实库里损坏 VPK 的正常噪声（`Given file is not a VPK.`）。

**未逐点修的同型代码**（依赖上面第 2 层兜底）：`VpkAddon.cs:197`、`WorkshopVpkAddon.cs:439`、`AddonRoot.cs:397`、`AddonRoot.cs:465`（都是"continuation 里清 Task 属性"，用的是 `Root.TaskScheduler` 串行调度器，仍是后台线程）。

### 3.12 P2 遗留 🟡 的复验

- `InputTextAsync`：**仍未取得界面证据**（P3 没有调用方）。
- `ShowOperationInterruptAsync`：**仍未取得界面证据**。
- `PickFileAsync`：**仍未取得界面证据**（P3 没有文件选择入口）。
- `ShowDeletionProgress`：删除流程走通，但删除量小（1 项）、进度窗一闪而过，**未单独留证**。

→ 三条继续顺延 P4/P5；P3 只证实了 `ConfirmAsync`（删除确认）与 `ShowInfoAsync`（占位提示）两条新证据。

### 3.13 P2 硬约束的回写

| P2 约束 | 结论 |
|---|---|
| #8「ViewLocator 未定型，MainWindow 里 Explorer 是占位，P3 必须替换并回写结论」 | **已结清**：不用 ViewLocator。`MainWindow.xaml` 直接 `<local:AddonNodeExplorerView DataContext="{x:Bind ViewModel.AddonNodeExplorerViewModel, Mode=OneWay}" Visibility="{x:Bind local:XBind.VisibleIfNotNull(ViewModel.AddonRoot), Mode=OneWay}" />`；Section 视图由 `AddonNodeView.xaml.cs` 按 VM 类型 `new` 并设 `DataContext`（同 Alt 做法）。**VM 不会自己 `Activate()`，激活由 `Controls/ViewModelLifecycleBinder` 驱动** |
| #1 对话框只走 `IDialogService` | 遵守（删除确认框实测） |
| #7 `WindowService` 占位逐个替换 | 12 个占位未动；P3 因详情面板需要**新增 3 个占位**（标签编辑器 / 自定义图片 / 节点选择器），共 15 个，全部仍走 `OpenNotPortedYet` |
| #2/#3 窗口 `Closed` 置位、后台窗口旁路 | 本阶段未新增窗口 |
| #4 新目录 csproj 显式 `CopyToOutputDirectory` | 本阶段无新资产目录 |
| #5/#6 只跑 `bin\x64\Debug`、自定义类型不用 `required` | 遵守 |


---

## 4. 风险联动

| 风险号 | 变化 | 理由 |
|---|---|---|
| R1（VM 行为漂移） | ↑ 上调（维持"高"） | P3 又发现 2 处 P1 遗留 VM 缺陷（`CurrentGroup` 不刷新、排序分支吞异步结果），都是"能跑但行为不对"的类型；P4/P5 的 41 个 VM 必须继续逐文件对照 |
| R2（虚拟化性能） | → 维持"低-中" | 真实库根只有 1 个组，无法造 2000 项场景复测；`ItemsWrapGrid` 原生虚拟化按设计可用，但**未取数** |
| R3（样式观感差异） | → 维持"低" | 本阶段不再逐样式还原，Fluent 原生优先 |
| R5（阻塞操作落 UI 线程） | ↓ 下调 | 加载真实库（含损坏 VPK 批量检查）期间界面不卡，`IUiDispatcher.Post(Low)` 生效 |
| R8（`CanExecute` 灰态） | → 维持"中" | 本阶段只顺带看到"无选择时移动/删除不可用"，**未做全量灰态专项** |
| R11（`ObservableObject` 撞名） | → 关闭后保持 | 无复发 |
| R17（双输出树） | → 维持 | 仍只认 `bin\x64\Debug`；本轮又踩到一次（`Stop-Process` 忘了先杀进程导致 `MSB3027` 文件占用） |
| **R19** | ✚ **新增** | `PathIcon.Data` 不能绑定；`XamlReader` 解析出的 `Geometry` 不能迁移给第二个元素。图标一律走 `Icons.cs` 字符串 + `IconView` |
| **R20** | ✚ **新增** | 派生 VM 用 `new` 遮蔽基类同名属性 → WinUI 反射绑定**静默失效**，整条 `{Binding Addon.*}` 链全废 |
| **R21** | ✚ **新增** | Core 后台线程触发 `PropertyChanged` → WinUI 抛 `COMException 0x8001010E`；`ObservableObject` 已加回投，但**对象在后台线程构造时兜底无效** |
| **R22** | ✚ **新增** | WinUI 会夺走 Explorer 的键盘焦点（清空选择 → 焦点落到主窗口其它控件），快捷键需窗口级 fallback |
| **R23** | ✚ **新增** | 集合型派生属性用"一次性快照"会吞掉异步结果；必须用活集合（`SortedObservableCollection`） |

---

## 5. 对下一阶段的硬约束（照做清单）

1. **图标一律 `<ctl:IconView Data="{x:Bind local:Icons.X, Mode=OneTime}"/>`**。禁止 `{Binding}`/`{x:Bind}` 到 `PathIcon.Data`；禁止把路径数据写成资源；禁止缓存 `Geometry` 分发给多个元素。
2. **派生 VM 不得用 `new` 遮蔽基类同名属性**。要窄化类型就用独有名字（`AddonGroup` / `RefAddon` / `VpkAddon` / `WorkshopAddon` 是现行命名），并同步所有 XAML/C# 调用点。
3. **新增 VM 属性前先看基类有没有同名属性**；有则改名，或改成在基类上做类型收窄。
4. **Core 里任何在后台 continuation 中修改可观察属性的代码，必须回投 `SynchronizationContext`**（`ObservableObject` 只在"对象在 UI 线程构造"时兜底）。新建"在后台线程构造、随后被 UI 绑定"的模型时，必须显式回投。
5. **文本框的实时行为一律加 `UpdateSourceTrigger=PropertyChanged`**（搜索、校验、过滤）；只在失焦写回会表现为"功能没反应"。
6. **集合型派生属性必须用会发通知的集合**（`SortedObservableCollection` 或 `ObservableCollection`），不要 `new ObservableCollection(source.Order(...))` 快照；同理，**可见性绑 `Count` 而不是绑集合本身**。
7. **任何窗口/视图级快捷键都要挂窗口级 fallback**：`XamlRoot.Content.AddHandler(KeyDownEvent, …, handledEventsToo: false)`，并在 `Unloaded` 移除；先判 `e.Handled` 保证 `TextBox` 优先。
8. **WinUI 没有 `ItemsControl.ContainerFromElement`**：反查容器用自写 `FindAncestor<T>(VisualTreeHelper.GetParent 循环)`。
9. **`MenuFlyoutSubItem` 必须点击展开**（hover 不展开）；`MenuFlyout` 不在视觉树里，菜单项一律用 `Click` 处理器，不要绑命令。
10. **窗口/对话框只经 `IWindowService` / `IDialogService`**（P2 约束 #1 仍然有效）；P3 新增的 3 个入口（标签编辑器 / 自定义图片 / 节点选择器）仍是占位，P4/P5 必须逐个替换掉 `OpenNotPortedYet`。
11. **验证只跑 `bin\x64\Debug\net10.0-windows10.0.26100.0\FireAxe.exe`，且 `Start-Process` 必须带 `-WorkingDirectory`**，否则 `ExportedAssets` 相对路径解析失败，日志也会落到解决方案根目录。
12. **构建前先 `Stop-Process -Name FireAxe -Force`**，否则 `MSB3027/MSB3026` 文件占用。
13. **自动化验证用 `mcp__windows__*`**；拖拽类交互必须用真实相对鼠标移动（`mouse_event(MOUSEEVENTF_MOVE)`），`SetCursorPos` 与 MCP `drag` 都不产生 `WM_POINTER`。
14. **窗口布局（150% DPI）**：截图 image 坐标 ×1.3333 = 屏幕坐标；操作 FireAxe 时把窗口固定在 (0,0)-(1200,760)，不得遮挡右下角 bilibili。

---

## 6. 遗留项（分级）

> 2026-11-10 阶段编号调整：原 P6 收尾现为 P7，下表归属同步顺延；新增 P6 为原生界面重设计。原实测结果保持不变，P6 可提前验证改动范围内的项目并补证据。

| 级别 | 项 | 归属 |
|---|---|---|
| 🟥 阻断 | 无 | — |
| 🟨 可延后 | 标签过滤面板（`AddonTagCheckBoxList`）只验证到渲染，未验证筛选生效 | P7 验收清单（"标签筛选"） |
| 🟨 可延后 | `InputTextAsync` / `ShowOperationInterruptAsync` / `PickFileAsync` 仍无界面证据（P2 遗留三次顺延） | P4/P5 第一次用到时必须现场复验 |
| 🟨 可延后 | 虚拟化性能未按真实大目录复测（R2 不关闭） | P7 |
| 🟨 可延后 | 图标"每实例解析一次 XAML"的运行时开销未测量；若 P4/P5 出现列表卡顿，优先改 `IconView` 为共享 `Path.Data` 之外的做法（例如缓存 `Geometry` 的**克隆**或改 `FontIcon`） | P7 |
| 🟨 可延后 | `IWindowService` 新增 3 个占位入口（标签编辑器 / 自定义图片 / 节点选择器） | P4 |
| ⬜ 观察 | 瓦片极窄时名称逐字换行（`P3rename` / `d`） | C 类打磨 |
| ⬜ 观察 | 移动条在极窄宽度下第二个按钮被裁（已给 `AddonRefView` 设 `Width=220`） | C 类打磨 |
| ⬜ 观察 | 侧键返回在列表项上方无效（需在空白区） | 与 Alt 行为等价，暂不动 |
| ⬜ 观察 | 全量 `CanExecute` 灰态未做专项核查（R8） | P7 验收清单 |

---

## 7. 附：实测证据

**某次运行样例，数值随环境与库内容浮动。**

启动（必须带工作目录）：

```powershell
Stop-Process -Name FireAxe -Force -ErrorAction SilentlyContinue
Start-Process "<REPO_ROOT>\FireAxe.WinUI3\bin\x64\Debug\net10.0-windows10.0.26100.0\FireAxe.exe" `
  -WorkingDirectory "<REPO_ROOT>\FireAxe.WinUI3\bin\x64\Debug\net10.0-windows10.0.26100.0"
```

日志：`…\bin\x64\Debug\net10.0-windows10.0.26100.0\Logs\Log20261108.txt`

**E1 瓦片尺寸随档位变化（150% DPI，UIA `BoundingRectangle`）**

| 档位 | `TileViewSize` | 实测外框 |
|---|---:|---|
| 小瓦片 | 75 | 219 × 222 |
| 瓦片 | 100 | 256 × 259 |
| 大瓦片 | 150 | 331 × 334 |

换算 ≈ `(ImageSize + 71) × 1.5`。

**E2 多选**：UIA `SelectionItemPattern.Select()` 三次 → 详情面板 `已选中3个项目。` + `基底, HUD&击杀反馈, 脚本&插件`，3 个 `ListItem` 的 `IsSelected` 均为 `true`。

**E3 详情面板（工坊 VPK，节选）**：名称 `Time to Die (Campaign)`、优先级 0、全局优先级 91、标签 `Campaigns/Single Player/Co-op`、文件大小 `262.6MiB`、创建时间 `2026/10/12 20:37:57`、
VPK 信息（版本 9 / 标题 / 作者 `KyleTheWalrus`）、工坊项目 ID `2257961887`、浏览量 `742371`、订阅量 `322676`、收藏量 `34575`、所有订阅量 `778263`、所有收藏量 `40714`、底部 `发现7个问题。`。

**E4 搜索**：进组 → 输入 `HUD` → 列表仅 `HUD&击杀反馈`；清空 → 11 项恢复。

**E5 移动往返（磁盘证据）**

```
<WORKSPACE_ROOT>\tmp-p3-root\P3renamed\未命名组   # 进组 Ctrl+V 后
<WORKSPACE_ROOT>\tmp-p3-root\未命名组             # XButton1 返回根，Ctrl+V 后
```

**E6 删除**：`Delete` → 对话框「确认删除 / 你确定要删除选中的项目吗？此操作无法撤销。/ 是 否」→ 点「是」→ `tmp-p3-root` 下目录消失。

**E7 改名**：`F2` → `PART_TextBox` 带原值获焦 → `SetValue("P3renamed")` + `Enter` → 列表 / 详情 / 磁盘目录名同步变更。

**E8 分栏拖拽**：`mouse_event(MOUSEEVENTF_MOVE, -20, 0)` × 7 → 详情列宽 465.33（clamp 上限），搜索框保留。

**E9 跨线程崩溃（修复前 / 后者为修复后）**

```
2026-11-08 23:06:44.121 [ERR] Unobserved task exception occurred
System.Runtime.InteropServices.COMException (0x8001010E)
  at FireAxe.ObservableObject.NotifyChanged(String propertyName) in …\FireAxe.Core\ObservableObject.cs:line 29
  at FireAxe.AddonNode.set_CheckTask(Task value) in …\FireAxe.Core\AddonNode.cs:line 194
```

修复后（真实库加载触发成批 VPK 检查）：

```
--- new lines: 20 ---
--- cross-thread count: 0
--- errors ---
[ERR] Exception occurred during reading the VPK file: …\NekoShaders & L4N-time_updated-1791462722.vpk
[ERR] Exception occurred during reading the VPK file: …\L4D2Evolved-time_updated-1794063704.vpk
[ERR] Exception occurred during reading the VPK file: …\Skeeto …-time_updated-1794052432.vpk
[ERR] Exception occurred during AppGlobal.GetLatestVersionAsync   （403 rate limit，环境噪声）
```

**E10 构建**：`dotnet build FireAxe.sln -c Debug` → `0 个警告 / 0 个错误`。

**E11 冒烟测试**：`dotnet test FireAxe.Tests\FireAxe.Tests.csproj -c Debug` →

```
已通过! - 失败: 0，通过: 9，已跳过: 0，总计: 9，持续时间: 1 s
```

**E12 版本号**：`Directory.Build.props` 的 `Version` 由 `0.0.0` 提到 `0.1.0`（按 §9 注：P3 完成记 0.1.0），启动日志确认 `FireAxe Start (Version: 0.1.0)`。

**关于仓库卫生**：本阶段调试期写的一批 UIA 探针脚本（`tools\*.ps1`）只用于取证，已于收尾删除；
`<USER_DOCS>\FireAxe\Settings.json` 的 `LastOpenDirectory` 已从测试根 `<WORKSPACE_ROOT>\tmp-p3-root` 改回真实库 `<LOCAL_ADDONS_DIR>`。
