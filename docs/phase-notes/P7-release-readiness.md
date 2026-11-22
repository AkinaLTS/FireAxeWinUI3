<!-- Source: original P7 implementation and local verification record. -->
# P7 · 收尾与发布准备（首轮）

> 阶段：P7 · 收尾；日期：2026-11-11（分发复核与 CI 复核 2026-11-14）。
> 环境：Windows 10.0.26340 x64；.NET SDK 10.0.402 / Runtime 10.0.13。
> 依赖：Windows App SDK 2.6.5；MVVM 8.4.2；SettingsControls 8.2.251219；MSIX BuildTools 1.7.251221100。
> 结论：**🟡 首轮可靠性与分发实现落地，P7 仍在实施，不能作为最终验收或 0.5.0 发布依据。**
> 2026-11-14 复核：干净检出构建已修复（`app.manifest` 曾被 `.gitignore` 的 `*.manifest` 排除，远端必报 CS1926）；PE 读取拒绝未复现；远端 CI 修复中（`dotnet test` 在 hosted windows-2025 上无输出挂起直至 45 分钟作业超时）。

## 结论速览

| 项目 | 结论 | 对计划的影响 |
|---|---|---|
| 客户端 DIP 换算、异常值与工作区约束 | ✅ 单元验证；🟡 实机缩放待验 | 100/125/150/200% 公式覆盖，不代表跨屏测试 |
| 搜索与图片生命周期 | 🟡 修复快速搜索订阅及并发刷新竞争、过期图片回投 | 保留重复回归与真实图片库验收 |
| 推送权限 | ✅ 当前测试进程在临时游戏目录创建/清理符号链接；🟡 普通用户策略待验 | 保持 asInvoker，失败提示开发者模式/手动管理员启动 |
| 发布/CI | 🟡 双 self-contained publish、许可证及 PRI 检查通过；2026-11-14 复跑全流程 exit 0（含 PE 检查、ZIP、SHA-256）；远端 CI 仍 🟡 | 2026-11-11 记录的 PE「Access denied」未复现（降为 🟨）；hosted run 在 `dotnet test` 处挂起并超时，修复待验证 |
| 观感、快捷键、全页面回归、滚动性能 | 🟡 缺实机证据 | §5 全量验收清单保持未勾选 |

## 逐项详述

### V1 · DPI、快捷键与隔离配置

使用 `WindowGeometry.GetClientSize` 换算客户端 DIP，校验有限正数、最小尺寸与工作区上限；`MainWindow` 使用最近显示器工作区扣除非客户端框架后恢复并居中。回归测试覆盖 1/1.25/1.5/2 倍率及损坏/超大尺寸。快捷键沿用 Ctrl+X/V/F、Delete、F2，尊重 TextBox/PasswordBox/RichEditBox 及 CanExecute。没有实际按键和跨屏操作证据，交互结论为 🟡。

新增绝对路径 `FIREAXE_DATA_DIRECTORY`：设置、日志、单实例按目录隔离；非法值不回退到真实用户目录。启动配置/日志初始化纳入既有入口异常处理。启动异常交互本轮没有故意触发。

### V2 · 搜索与图片

**B 级问题：异步搜索偶发空结果。** 2000 节点测试曾在 15 秒后报 `Assert.Single() Failure: The collection was empty`。第一处原因是查询早于排序视图订阅启动；修改为先连接视图再启动查询。后续复测仍失败，发现无 UI dispatcher 的两个独立 debounce 可并发刷新，取消/结果集合/代次发生交错，补充刷新事务锁；结果消费与完成也使用同一锁。保留失败和最终验证记录，不通过延长超时或删断言掩盖问题。随后独立运行 P7 回归五次，每次 8/8、合计 40/40 通过，均包含该搜索场景（原始 TRX 见附录索引）；继续作为观察项。

图片通过 WinRT 操作的 `AsTask(cancellationToken)` 取消，UI 队列执行时复查 refreshId 与激活状态并释放过期结果。现有每 VM 原始图片缓存及 ListView 虚拟化保留；未测真实图片解码、内存峰值和 2000+ 滚动。

### V3 · 推送权限

BCL `File.CreateSymbolicLink` 在实际 addons 目录做随机临时链接预检查，成功后才清除旧推送链接。临时游戏目录测试验证探针清理、旧 fireaxe_link 清理、非托管文件/目标文件保留、addonlist 生成。这里只证明当前测试进程有权限；不能推断普通用户、开发者模式关闭或受管设备策略。预检查不能保证后续磁盘/占用错误的事务回滚。

### V4 · 发布与兼容性

**A 级问题（已改实现）：发布缺少应用 PRI。** MSIX tooling 关闭时，BuildTools 1.7.251221100 的 MRT targets 生成 PRI，却没有加入 .NET 发布集合。工程兼容 target 使用 SDK 已生成路径/元数据补入；不扫描旧 bin。Release 默认将 XBF 嵌入 PRI，因此使用 SDK makepri 验证 App/MainWindow/AddonNodeExplorerView 的 EmbeddedData，不能要求独立 XBF 文件。

发布脚本生成独立目录，执行 Release build/test、双 self-contained publish、原始许可/NOTICE 收集、PE 静态导入检查、ZIP 文件数与 SHA-256。静态检查依赖构建机 System32，忽略 delay-load、COM 和动态加载，不能替代干净机器。

SettingsControls 的 Windows 资产要求 19041，因此应用和测试的最低平台统一为 Windows 10 2004。尚未在 Windows 10 或干净 VM 启动。MSIX 延后。

### V5 · 分发复核与远端 CI（2026-11-14）

**🟡 本地全绿，远端未达标。**

- 干净检出构建：远端两次运行都停在 CS1926（找不到 `FireAxe.WinUI3\app.manifest`）。根因是 `.gitignore` 第 399 行 `*.manifest`（PyInstaller 模板段）把应用清单排除在版本控制外；本地因文件始终在磁盘上从未暴露。修复：强制纳入版本控制并加注释，提交 `f518da3`。
- 本地复跑：`pwsh -File ./scripts/Publish-Windows.ps1 -ArtifactRoot artifacts/p7-ci-probe` → exit 0、Release 0 警告 0 错误、18/18 测试、ZIP 99,625,003 字节；`-SkipTests` 复跑 → exit 0、540 个发布文件、ZIP 95 MB、SHA-256 与 manifest 齐全。2026-11-11 记录的 PE 读取拒绝**未复现**（详见 [access-denied 复核](../evidence/P7/ci-2026-11-14/02-access-denied-reread.md)）。
- 远端 CI（hosted `windows-2025`，run 43566717367 attempt 1/2 与 43573415475）：步骤 4 前全部通过，`dotnet build` Release 0 错误 1.5 分钟内完成，随后 `dotnet test` 打印 `Test run for … FireAxe.Tests.dll` 与 `A total of 1 test files matched` 后**再无输出**，直到 45 分钟作业超时被取消（attempt 2：07:32:28 → 09:01:49）。本机同样命令 8–10 秒完成，说明是本机未复现的 runner 侧挂起，不是测试失败。
- 已落地缓解（未验证）：新增 `scripts/Test-WindowsTests.ps1`，用 `Start-Process` 重定向输出并在 10 分钟硬超时后杀掉测试进程（`& dotnet test | Tee` 会在测试宿主不退出时永久阻塞管道）；工作流把测试提前成独立步骤、发布步骤改用 `-SkipTests`，作业超时 45 → 90 分钟。

## 风险联动

| 风险 | 变化 | 理由 |
|---|---|---|
| R23 异步集合结果丢失 | ↑ 下调 | 补订阅顺序及刷新串行化；保留竞争复测 |
| R25 控件/性能/兼容性 | 🟡 保留 | 最低版本对齐 19041，真实图片/Windows 10 缺证据 |
| R24 P6 导航回归 | 🟡 保留 | Computer Use 关闭，无法实机点验 |
| R26 发布缺资源 | ✚ 新增后 ↑ 下调 | PRI 发布桥接与资源内容检查，干净机器仍需验证 |
| R27 干净检出构建阻断 | ✚ 新增后 ✖ 消失 | `app.manifest` 被 `.gitignore` 排除导致 CS1926；提交 `f518da3` 后本地与远端构建均通过 |
| R28 hosted CI 测试挂起 | ✚ 新增 | `dotnet test` 在 hosted windows-2025 无输出直至超时；已加限时脚本，待远端验证 |

## 对下一阶段的硬约束

1. 按 [实机验收记录表](P7-manual-acceptance.md) 继续 P6/P7 实机验收；不要因编译或数据单测通过勾选计划 §5。
2. 解压完整 ZIP，在干净 Windows 10 19041+ / Windows 11 验证运行；检查真实依赖与权限。
3. 以普通用户分别验证开发者模式开/关、不可写目录和旧链接保留。
4. 使用真实 2000+ 图片插件库测滚动、三视图、解码和内存；记录条件、容器数与峰值。
5. 完成深浅色/高对比度、材质降级、四档 DPI、多屏、返回焦点、多选、全部工具页面和设置重启往返。
6. 保持版本 0.1.0；全部验收后再评估 0.5.0。代码与本记录必须同一提交。

## 遗留项（分级）

| 项 | 级别 | 归属 | 备注 |
|---|---|---|---|
| 发布 EXE 读取拒绝 | 🟨 可延后（观察） | P7 | 2026-11-11 记录的错误路径实为 `obj\…\win-x64\apphost.exe`（MSB3021 文件锁），2026-11-14 复跑未复现；见 [复核](../evidence/P7/ci-2026-11-14/02-access-denied-reread.md) |
| 远端 CI 构建与测试 | 🟥 分发验证阻断 | P7 | 干净检出已修（`f518da3`）；`dotnet test` 在 hosted windows-2025 挂起，限时脚本待远端验证 |
| UI/全量回归与 P6 未验收项 | 🟥 最终发布阻断 | P6/P7 | Computer Use 关闭，不能自报通过 |
| 干净 Windows、普通权限与 hosted CI | 🟥 最终发布阻断 | P7 | 当前仅本地构建环境 |
| 真实图片库滚动/内存/跨屏 | 🟥 最终发布阻断 | P7 | 2000 组数据测试不代表 UI 性能 |
| 可选 MSIX | 🟨 可延后 | P7 后 | 默认 ZIP 路线 |
| 搜索并发刷新 | ⬜ 观察 | P7 | 保留失败记录与重复测试 |

## 附：实测证据

源码回归见 `FireAxe.Tests/P7RegressionTests.cs`；发布命令：

```powershell
pwsh -File scripts/Publish-Windows.ps1 -ArtifactRoot artifacts/p7-final
```

最终原始日志、测试结果、资源/依赖检查与启动记录索引见 [验证记录](../evidence/P7/final-validation.md)。数据性能数值为本机某次样例，随负载浮动；没有 UI 渲染指标。历史失败记录保留在本机 artifacts/p7-final 各次独立目录，原始 15 秒失败已在 V2 如实记录。

2026-11-14 复核的原始证据：

- [步骤 5 原始日志](../evidence/P7/ci-2026-11-14/01-dispatch-run-step5.log)：hosted run 43573415475 的 `dotnet test` 在 08:19:14 打印测试文件后直到 09:01:49 无输出。
- [PE 读取拒绝复核](../evidence/P7/ci-2026-11-14/02-access-denied-reread.md)：比对 2026-11-11 证据文件原文与本机复跑结果。
- 已删除远端两次失败的临时运行记录（43566717367、43573415475）；打包修复未推送前工作流已手动停用（`disabled_manually`），推送后需重新启用。

