<!-- Source: original changelog for the WinUI 3 migration. -->
# 更新记录

## 未发布 · P7 进行中（基于 0.1.0）

- 增加 Windows x64 zip 发布脚本：同时携带 .NET 和 Windows App SDK，收集许可证与 NOTICE，生成 SHA-256、静态依赖报告和发布清单。
- 增加 GitHub Actions 构建、测试和产物上传；显式版本标签或手动选择已有标签时创建 Release。
- 窗口尺寸按客户端 DIP 与物理像素换算；恢复时约束在当前显示器工作区内并居中，防止异常保存值溢出。
- 快捷键尊重文本编辑器焦点，并在命令可用时执行移动和删除。
- 图片解码向 WinRT 操作传递取消信号；回投 UI 时重新检查视图生命周期，释放过期结果。
- 搜索先建立结果排序订阅再启动后台查询，避免快速结果丢失；取消后的排队结果不会继续写入。
- 推送删除旧链接前，先使用 .NET 符号链接 API 检查实际目标目录；权限失败时提供中英文处理提示。
- 增加 `FIREAXE_DATA_DIRECTORY` 绝对路径配置，设置、日志与单实例按数据目录隔离。
- 最低 Windows 版本统一为 Windows 10 2004（19041），对应 SettingsControls 依赖资产。
- 增加 DPI、异常尺寸、隔离路径、临时游戏目录推送与 2000 节点数据回归。
- 修复干净检出无法构建：把 `FireAxe.WinUI3/app.manifest` 纳入版本控制（此前被 `.gitignore` 的 `*.manifest` 规则排除，CI 必报 CS1926）；README 拆分为简体中文 `README.md` 与英文 `README.en.md`。

P6/P7 的实机主题、焦点、多显示器、全工具页面和真实图片库性能验收仍未完成；版本号暂保持 0.1.0，不作为 0.5.0 正式发布。

## 0.1.0 · 移植开发基线

P0～P3 验证主界面和业务抽象；P4/P5 工具页面有条件完成，P6 原生导航、分类设置与单主窗布局首轮落地。历史原始证据见 [阶段结论索引](docs/phase-notes/README.md)。
