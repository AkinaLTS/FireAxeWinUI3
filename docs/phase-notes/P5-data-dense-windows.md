# P5 · 工具窗第二批（数据密集）

> 阶段：P5 · 工具窗第二批（见 `PORTING_PLAN.md` §2）
> 日期：2026-11-10
> 环境：Windows 11 · .NET SDK 10.0.402 / Runtime 10 · WinUI 3 / Windows App SDK 2.6.5
> 依赖：CommunityToolkit.Mvvm 8.4.2 · 非打包 x64 Debug
> 结论：**三个重窗及关联逻辑已接入；构建、自动化测试和 exe 启动通过。受限于桌面菜单焦点不稳定，独立工具窗交互验收未完成，整体有条件完成。**

---

## 结论速览

| # | 验证项 / 交付项 | 结论 | 对计划的影响 |
|---|---|---|---|
| V1 | FileCleaner、ReplacementOverview、WorkshopVpkFinder 视图与 WindowService 路由 | ✅ 通过 | 三个 P5 路由已实现；进入 P7 回归 |
| V2 | VPK 冲突详情双向列表及异常/问题解释逻辑 | ✅ 通过 | 相关控件、注册及 Alt 映射已核对 |
| V3 | 全量 Debug 构建 | ✅ 通过 | 0 警告、0 错误 |
| V4 | FireAxe.Tests 全量自动化测试 | ✅ 通过 | 9 通过、0 失败、0 跳过 |
| V5 | 生成 exe 启动 | ✅ 通过 | 主窗口启动并保持运行；不等同于工具窗功能验收 |
| V6 | 三个工具窗实际交互与视觉点验 | 🟡 有条件通过 | 延至 P7；当前仅确认工具菜单包含入口，未能稳定激活窗口 |

**总判断：** 可进入新增 P6 原生界面重设计，原收尾回归及遗留顺延为 P7。P5 代码、构建及自动化验证完成；不把启动主窗口或菜单可见误记为三个工具窗交互通过。

> 2026-11-10 编号调整：原 P6 收尾现为 P7，本文回归与遗留归属同步更新；本次不改变原实测结论。P6 对改动范围内的交互负责，P7 负责最终全量验收。

---

## 逐项详述

### V1 · 重窗视图与路由

**✅ 通过。** 源视图需求与 WinUI 实现逐项核对，并检查 `WindowService` 路由及三个 code-behind 生命周期：FileCleaner 提供全局/按来源搜索、四种查找、暂存项和容量显示；ReplacementOverview 提供过滤、摘要刷新、分类/槽位展开及定位；WorkshopVpkFinder 提供目录扫描、工坊 ID/标题状态、网页打开、移除和创建。窗口均设置 VM 与 `ViewModelLifecycleBinder`，关闭时解绑窗口事件并停用/释放 VM。

原始证据：`FireAxe.WinUI3/Services/Impl/WindowService.cs:122-140` 构造三个窗口和 VM；`FireAxe.WinUI3/Views/FileCleanerWindow.xaml.cs:16-52`、`ReplacementOverviewWindow.xaml.cs:11-37`、`WorkshopVpkFinderWindow.xaml.cs:13-33` 显示生命周期与 picker/close event；对应三个 XAML 视图已纳入工程并通过构建。

**B 级坑：** WinUI XAML 编译器无法解析若干嵌套 VM 数据类型，且模板中的 `x:Bind` 要求 `x:DataType`。模板改用普通 `{Binding}`；FileCleaner 文件容量改用 `ReadableBytesConverter`，避免 `XBind.ReadableBytes(long?)` 与 `ReadableBytes(double)` 生成调用二义性。改后构建通过。

### V2 · 冲突详情和解释逻辑

**✅ 通过。** `VpkAddonConflictingDetailsView` 已用于 VPK 冲突列表及插件详情区段；其冲突文件忽略、忽略列表移除和批量命令均有对应绑定。CrashReporter、ExceptionExplanations、ObjectExplanationManager、AddonProblemExplanations 与 AddonProblemTypeExplanations 在 WinUI 工程存在，并与只读 Alt 对照：11 个问题解释注册（含 AddonChildrenProblem），10 个类型文案映射一致；派生异常通过基类查找处理。

原始证据：`FireAxe.WinUI3/Views/VpkAddonConflictListWindow.xaml:36-43`、`VpkAddonSectionView.xaml:87`；`FireAxe.WinUI3/Program.cs:26-29,77-82` 在启动 UI 前注册解释；映射见 `FireAxe.WinUI3/AddonProblemTypeExplanations.cs:10-33`；注册见 `AddonProblemExplanations.cs:8-30`。Alt 对照文件为 `FireAxe_Alt/FireAxe.GUI/AddonProblemTypeExplanations.cs` 与 `AddonProblemExplanations.cs`，内容映射一致。CrashReporter 在 WinUI 目标中省略 Linux 分支，符合 Windows-only 目标。

### V3 · 全量 Debug 构建

**✅ 通过。** 在仓库目录运行：

```powershell
 dotnet build FireAxe.sln -c Debug
```

本阶段最后一次构建输出：`已成功生成。`、`0 个警告`、`0 个错误`。目标 exe：`FireAxe.WinUI3/bin/x64/Debug/net10.0-windows10.0.26100.0/FireAxe.exe`。

**B 级坑：** FireAxe 运行时会锁定输出 exe，重建前需关闭进程；关闭后构建成功。避免在运行窗口仍打开时把 MSB3027/MSB3021 锁文件当成代码错误。

### V4 · 自动化测试

**✅ 通过。** 执行 `dotnet test FireAxe.sln -c Debug --no-build`，输出：`已通过! - 失败: 0，通过: 9，已跳过: 0，总计: 9`。此前 `AddonNodeExplorerViewModelTests.SearchText_FiltersCurrentNodes` 曾在一轮全量运行中偶发失败（断言 `the search never narrowed the list to Alpha`），单测筛选重跑通过，全套再次运行 9/9 通过；未改动该行为，P5 无可复现回归。

### V5 · exe 启动

**✅ 通过（启动级）。** 从上述 x64 Debug 输出运行 `FireAxe.exe`，进程启动并显示主窗口 `FireAxe - <LOCAL_ADDONS_DIR>`。工具菜单可见“替换总览”“创意工坊VPK查找器”“文件清理器”。这证明启动与入口呈现，不证明窗口自身内容及业务操作正确。

### V6 · 工具窗交互验收

**🟡 有条件通过。** 已打开桌面控制并确认主窗、工具菜单和三个入口，但自动化点击后焦点多次转移至 Harness、Terminal 或 Windows 通知/日历面板，无法可靠证明目标窗口已打开及操作生效。因此不记录截图为窗口验收证据，逐窗内容、命令、选择器、空态和关闭行为留待 P7 在稳定桌面焦点下验证。

---

## 风险联动

| 风险号 | 变化 | 理由 |
|---|---|---|
| R1 | ↓ 下调 | 三个重窗 UI 与 VM 已编译接线，且全量测试通过；业务交互仍需 P7 验证 |
| R6 | ↑ 上调 | UI 自动化/桌面点验不稳定，本阶段无法以可靠交互证据覆盖窗口行为 |
| R9 | ↓ 下调 | 新窗口统一使用生命周期绑定器并在 Closed 清理；仍保留真实关闭/重开回归 |
| R14 | ↑ 上调 | 新窗模板再次触发嵌套类型、x:Bind 生成错误；按普通 Binding 与资源 Converter 修复后恢复 |

---

## 对下一阶段的硬约束（照做清单）

1. 逐一打开 FileCleaner、ReplacementOverview、WorkshopVpkFinder，验证窗口显示、关闭、重开及 VM 生命周期。
2. 用可控测试目录验证 FileCleaner 四种查找、来源选择、全局搜索、暂存/保留/删除和容量更新；避免对真实用户文件执行破坏性删除。
3. 验证 ReplacementOverview 过滤、刷新、分类/槽位展开及定位跳转；验证 Workshop 扫描、加载/失败状态、打开网页、移除和创建。
4. 验证 VPK 冲突详情中的单项/批量忽略、移除及空态；覆盖插件详情区段和冲突列表两种宿主。
5. 按 `PORTING_PLAN.md` §5 补做 P4 遗留和主链路回归；启动/菜单可见不得替代业务点验。
6. 重建前关闭 FireAxe 进程；继续使用 x64 Debug 输出路径。

---

## 遗留项（分级）

| 项 | 级别 | 归属阶段 | 备注 |
|---|---|---|---|
| 三个新工具窗逐项交互、视觉和生命周期验证 | 🟨 可延后 | P7 | 本阶段桌面自动化焦点不稳定；菜单有入口但未取得窗口操作证据 |
| VPK 冲突详情单项/批量操作实机验证 | 🟨 可延后 | P7 | 静态绑定已核对，业务操作未实测 |
| P4 窗口逐项点验与 AddAddonTag 自动补全差异 | 🟨 可延后 | P7 | 延续 `P4-tool-windows.md` 遗留 |
| D8：asInvoker 下的符号链接推送及失败诊断复验 | 🟨 可延后 | P7 | 本阶段未执行推送，不认定权限策略已通过复验 |
| 搜索测试偶发失败的并发/时序条件 | ⬜ 观察 | P7 | 本次独立测试及后续全套测试均通过，当前未复现 |

---

## 附：实测证据

> 以下为 2026-11-10 此环境的一次运行样例，数值随机器与负载浮动。

```text
命令：dotnet build FireAxe.sln -c Debug
结果：已成功生成。
警告：0 个警告
错误：0 个错误
输出：FireAxe.WinUI3\bin\x64\Debug\net10.0-windows10.0.26100.0\FireAxe.exe

命令：dotnet test FireAxe.sln -c Debug --no-build
结果：已通过! - 失败: 0，通过: 9，已跳过: 0，总计: 9，持续时间: 1 s

启动：Start-Process <FireAxe.exe> -WorkingDirectory <输出目录>
结果：主窗口可见并运行；工具菜单包含三个 P5 窗口入口。
交互验收：未完成；桌面自动化焦点在菜单操作时转移，不能将入口可见记为窗口通过。
环境：dotnet --version -> 10.0.402；WindowsVersion -> 2009。
源核对：AddonProblemExplanations 注册 11 种问题；AddonProblemTypeExplanations 映射 10 种类型；Alt 对照一致。
```
