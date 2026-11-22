![FireAxe Logo](FireAxe.WinUI3/Assets/AppLogo.ico)
# FireAxe（WinUI 3 版）

**简体中文** | [English](README.en.md)

FireAxe 是用于管理《Left 4 Dead 2》插件的开源图形工具。本仓库是 `FireAxe_Alt`（Avalonia 版，v0.7.6）的 **WinUI 3 重写**，目标是带上 Windows 原生的界面与交互，同时保留 Alt 的业务语义。

技术栈：WinUI 3 / .NET 10 / Windows App SDK 2.6.5 / CommunityToolkit.Mvvm 8.4.2。

> **状态：0.1.0 开发预览。** 主界面、工具页面、原生导航、分类设置与发布脚本均已落地，业务层测试全绿；视觉与交互的实机验收、干净机器启动验证尚未完成，因此还没有正式版本。阶段进度与遗留项见 [移植计划](docs/PORTING_PLAN.md) 与 [阶段结论索引](docs/phase-notes/README.md)。

## 功能

与 Alt 一致：

- 通过“组”层级式管理插件，支持引用插件快速切换游玩方案
- 创意工坊物品与合集下载
- 启用状态管理、优先级管理、依赖管理、标签管理
- VPK 冲突检测与插件问题检查
- 本地 VPK 文件管理、VPK 与工坊物品信息显示
- 插件导入导出（`.addonroot`）、搜索与过滤

WinUI 3 版的差别：

- Windows 原生单主窗：次级界面改为主窗内页面导航，不再弹独立窗口
- WinUI 3 风格的分类设置页与原生控件
- 数据密集型列表默认虚拟化，避免大库卡顿

## 使用发布包

支持 **Windows 10 2004（19041）及以上 / Windows 11，x64**。

1. 将 `FireAxe-<版本>-win-x64.zip` **完整解压**到一个可长期保留的目录。
2. 双击 `FireAxe.exe`。这是目录分发，不是单文件程序；移动时请移动整个目录。
3. 解压后的目录已自带 .NET 与 Windows App SDK 运行时，无需另行安装。

默认设置与日志保存在“文档/FireAxe”，日志在其 `Logs` 子目录。首次使用请先在设置里指定插件目录与游戏目录。

推送启用状态需要写入游戏目录并创建符号链接：建议开启 Windows“开发者模式”；若受系统策略限制，可关闭应用后手动以管理员身份运行。

## 从源码构建

需要 Windows、.NET SDK **10.0.4xx**、PowerShell 7 与 git-lfs；本机需安装 Windows App Runtime 2.6（开发构建按框架依赖模式启动）。

```powershell
git lfs pull
dotnet build FireAxe.sln -c Debug
.\FireAxe.WinUI3\bin\x64\Debug\net10.0-windows10.0.26100.0\FireAxe.exe
dotnet test FireAxe.Tests\FireAxe.Tests.csproj -c Debug -p:Platform=x64
```

应用工程固定 `Platform=x64`，直接构建工程与构建解决方案都输出到 `bin/x64/<配置>/...`。

生成发布包：

```powershell
pwsh -File .\scripts\Publish-Windows.ps1
```

脚本依次执行 Release 构建、测试、`win-x64` 双 self-contained 发布，收集项目与 NuGet 许可证、生成 NOTICE，检查静态原生依赖与应用资源（PRI），最后输出 zip、SHA-256 与发布清单；每次运行使用独立目录（默认 `artifacts/publish`），原始证据留在该次的 `evidence`。脚本不会启动应用。

## 常见问题

**为什么游戏里看不到刚启用的插件？** 确认插件自身与其所在的所有组都已启用，并点了推送；若推送时游戏正在运行，需在游戏菜单里刷新插件列表。

**怎么把工坊订阅的插件迁移过来？** 用工具菜单里的工坊 VPK 查找器；迁移后需要在创意工坊取消订阅，FireAxe 不管理已订阅的工坊插件。

**推送失败怎么办？** 确认文件系统是 NTFS 等支持符号链接的类型，并确认游戏目录可写；必要时以管理员身份运行。

**优先级怎么算？** 同名文件由优先级最高者胜出。每个插件有自身优先级，所在组的优先级会累加到全局优先级，最终生效的是全局优先级。

**能同时开多个实例吗？** 同一数据目录只允许一个实例，第二个会被重定向到已运行的窗口；用 `FIREAXE_DATA_DIRECTORY` 指定不同目录可并行运行多套配置：

```powershell
$env:FIREAXE_DATA_DIRECTORY = 'D:\FireAxe-TestProfile'
.\FireAxe.WinUI3\bin\x64\Debug\net10.0-windows10.0.26100.0\FireAxe.exe
```

**插件浏览区有哪些快捷键？**

| 按键 | 行为 |
|---|---|
| Ctrl+F | 聚焦搜索 |
| Ctrl+X / Ctrl+V | 标记移动 / 移动到当前位置 |
| Delete | 删除所选项 |
| F2 | 编辑单个所选项名称 |
| Enter / Esc | 名称编辑器提交 / 取消 |

## 工程结构

- `FireAxe.Core`：业务与文件格式，不依赖 UI。
- `FireAxe.WinUI3`：单主窗、Views、ViewModels 与平台服务。
- `FireAxe.Tests`：业务、ViewModel 与回归测试。
- `scripts`：本地与 CI 共用的分发脚本（`.github/workflows/build-and-release.yml`）。
- `docs`：[移植计划](docs/PORTING_PLAN.md)、[阶段结论索引](docs/phase-notes/README.md)、[原生界面设计](docs/P6_NATIVE_UI_DESIGN.md)。

`FireAxe_Alt` 是冻结的 Avalonia 基线，仅用于读取对照，所有修复都在本仓库完成；命名空间保持 `FireAxe` / `FireAxe.Views` / `FireAxe.ViewModels`。

CI 在分支与 PR 上只做构建、测试与产物上传；推送与 `Directory.Build.props` 版本一致的 `v<版本>` 标签，或手动选择已有标签并启用 `create_release`，才会创建 GitHub Release。

## 许可

Apache-2.0，见 [LICENSE.txt](LICENSE.txt)。发布包附带依赖清单与第三方原始许可文件。
