# P4 · 工具窗第一批

> 阶段：P4 · 工具窗第一批（见 `PORTING_PLAN.md` §2）
> 日期：2026-11-09
> 环境：Windows 11 · .NET SDK 10 · WinUI 3 / Windows App SDK 2.6.5
> 依赖：CommunityToolkit.Mvvm 8.4.2 · 非打包 x64 Debug
> 结论：**主工程与 P4 窗口可编译，exe 可启动；因未进行交互式窗口点验，整体有条件通过。**
>
> 2026-11-10 编号调整：原 P6 收尾顺延为 P7，本文遗留归属同步更新；新增 P6 为原生界面重设计。此次仅调整后续阶段引用，原实测结论不变。

---

## 结论速览

| # | 验证项 / 交付项 | 结论 | 对计划的影响 |
|---|---|---|---|
| V1 | P4 窗口及 WindowService 路由落地 | ✅ 通过 | P4 路由完成；P5 三个窗口占位保留 |
| V2 | 全量 Debug 解决方案构建 | ✅ 通过 | 0 警告、0 错误，可继续 P5 |
| V3 | 启动生成的 exe | ✅ 通过 | 进程保持运行并响应，需后续 UI 回归 |
| V4 | 窗口交互与 Alt 行为等价 | 🟡 有条件通过 | 需开启桌面控制后逐窗操作验证；AddAddonTag 自动补全差异需确认 |

**总判断：** 可进入 P5，但不能把 P4 的交互验收视为已完成；逐窗截图/操作和真实插件库功能回归列为 P7 遗留。AddAddonTag 的独立窗口折入标签编辑器输入流程，现有标签自动补全未移植。

---

## 逐项详述

### V1 · 窗口与路由

**✅ 通过。** 对照 `WindowService.cs` 与 P4 窗口列表核对路由；`OpenNotPortedYet` 仅用于 ReplacementOverview、WorkshopVpkFinder、FileCleaner 三个 P5 窗口。Settings、下载列表、About、问题列表、标签管理、VPK 冲突、名称自动设置、文件名修复、新建工坊合集、导入结果、标签编辑、自定义图片及插件选择均已有实现/路由。新窗口使用生命周期绑定器；下载列表由嵌入式 `DownloadItemListView` 驱动其 VM 生命周期。

P4 清单里的 `AddonImportResultWindow` 已在 P2 实现；`CheckingUpdateWindow` 与 `AppAlreadyRunningMessageWindow` 已在此前阶段实现。`AddAddonTagWindow` 被整合进 `AddonTagEditor` 的 `IDialogService.InputTextAsync` 输入流程，未新建独立窗口。

**C 级差异：** 独立 AddAddonTag 自动补全（包括已有标签建议）尚未实现；后续补齐并比较 Alt 语义。

### V2 · 构建

**✅ 通过。** 在项目目录运行：

```powershell
dotnet build FireAxe.sln -c Debug
```

输出证据：`已成功生成。`、`0 个警告`、`0 个错误`，耗时 `00:00:17.25`。目标输出位于 `FireAxe.WinUI3\bin\x64\Debug\net10.0-windows10.0.26100.0\FireAxe.dll`。

**B 级坑：** XamlCompiler 会将 `{x:Bind}` 不存在成员路径报告成 `WMC9999` 与本地化资源提示，实际堆栈在 `BindingPathListener.ResolveNameOnType`。核对 VM 成员并将运行期对象路径改为 `{Binding}` 后恢复构建；资源常量仍用 `{x:Bind}`。

### V3 · 进程启动

**✅ 通过（启动级）。** 使用 `Start-Process` 运行目标 exe，5 秒后进程仍存在；另一次查询报告 `Responding=True`，随后由验证命令关闭进程。这里只证明进程启动与存活，不证明窗口视觉和交互正确。

### V4 · 交互验收

**🟡 有条件通过。** 当前会话的 Windows Computer Use/桌面截图能力关闭，无法获取主窗口截图或实际点击 P4 入口。构建与进程启动不替代 UI 验收。需在桌面控制可用后，至少验证设置、问题/冲突、下载、标签、文件名修复、自动名称、图片选择、节点选择与新建合集的显示、关闭和关键命令。

---

## 风险联动

| 风险号 | 变化 | 理由 |
|---|---|---|
| R14 | ↑ 上调 | P4 XAML 绑定错误再次触发 WMC9999，修复需逐页定位成员路径 |
| R9 | ↓ 下调 | 本阶段新增窗口按统一生命周期绑定器激活/停用 VM；仍需桌面交互验证 |
| R6 | ↑ 上调 | 本阶段尚无自动化 UI 验收，桌面控制关闭导致交互证据缺失 |

---

## 对下一阶段的硬约束（照做清单）

1. 保留 P5 三个未移植窗口路由占位：ReplacementOverview、WorkshopVpkFinder、FileCleaner。
2. 所有新窗口必须用 `ViewModelLifecycleBinder`，并在 `Closed` 中解除窗口特有事件与资源。
3. 对 XAML `WMC9999` 先检查 `{x:Bind}` 成员路径和 VM 的公开属性，再考虑工具链问题。
4. 开桌面控制后逐窗点验 P4，并补录截图路径、命令和关键结果。
5. 补做 AddAddonTag 的已有标签建议/自动补全，或记录经确认的产品差异。

---

## 遗留项（分级）

| 项 | 级别 | 归属阶段 | 备注 |
|---|---|---|---|
| P4 窗口视觉与交互逐项点验 | 🟨 可延后 | P7 | 当前无桌面截图/点击证据 |
| AddAddonTag 已有标签建议与自动补全 | ⬜ 观察 | P7 | 当前整合输入流程，功能差异待确认 |
| 真实插件库上的工具功能回归 | 🟨 可延后 | P7 | 构建和进程存活不能覆盖业务行为 |

---

## 附：实测证据

> 以下为 2026-11-09 此环境的一次运行样例，数值随机器与负载浮动。

```text
命令：dotnet build FireAxe.sln -c Debug
结果：已成功生成。
警告：0 个警告
错误：0 个错误
耗时：00:00:19.04

命令：Start-Process <FireAxe.exe> -WorkingDirectory <输出目录> -PassThru; 等待 5 秒并查询进程
结果：RUNNING 22408 FireAxe
另一次检查：Responding=True；随后由验证命令停止该进程。

源码检查：WindowService.cs 中 OpenNotPortedYet 的调用仅为三个 P5 窗口。
交互证据：未采集；当前会话 Computer Use 已关闭。
```
