# P6 界面与入口映射

> 实施记录，2026-11-10。表内表示源码入口覆盖，不代表界面交互已通过。设置逐项清单另见 P6-settings-map.md（设置改造完成后产出）。

## 导航与上下文

主窗使用 NavigationView：插件库、工具、下载；设置和帮助在底部。插件库与其他内容页保持同一视图实例，仅切换 Visibility。切页不新建 MainWindowViewModel 或 Explorer VM；隐藏插件库时解除窗口级快捷键处理，返回时恢复。用户于 2026-11-11 实测后明确要求所有页面在主窗口内显示；原设置和工具的独立窗口路由正在改为单窗口页面宿主。该要求取代首轮沿用旧窗口路由的实现决定。

下载、设置、关于及工具页面已统一由主窗 SecondaryHost 承载；19 个次级视图使用 HostedPage（UserControl），保留原类名以减少绑定变更。共享下载队列保持单个视图宿主。常规页面缓存实例，切换 Content 触发卸载/加载；返回栈区分页面返回和插件组内返回。选择器、导入结果通过页面关闭事件完成异步结果，进度不可关闭时阻止导航。目录变化与主窗关闭清理缓存。最终构建 0 警告/错误、冒烟 9/9，已启动 PID 3972 供用户实测；这些结果不代表交互验收通过。

布局示意：主窗侧栏 → 内容页标题/说明 → 命令栏 → 浏览列表/详情 → 检查及推送状态；设置侧栏 → 分类标题/说明 → SettingsCard/SettingsExpander。

## 主窗原命令去向（27 项）

| 原命令 | 新入口 |
|---|---|
| OpenDirectoryCommand | 插件库命令栏；无目录空态按钮 |
| CloseDirectoryCommand | 插件库命令栏溢出 |
| ImportCommand | 插件库命令栏 |
| ImportAddonRootFileCommand | 插件库命令栏溢出 |
| OpenSettingsWindowCommand | 侧栏底部设置 |
| OpenDownloadListWindowCommand | 下载页 |
| OpenProblemListWindowCommand | 工具 → 检查；插件库问题状态 |
| OpenTagManagerWindowCommand | 工具 → 整理 |
| OpenVpkConflictListWindowCommand | 工具 → 检查 |
| OpenReplacementOverviewWindowCommand | 工具 → 检查 |
| OpenWorkshopVpkFinderWindowCommand | 下载页 |
| OpenFileCleanerWindowCommand | 工具 → 文件清理 |
| OpenAddonNameAutoSetterWindowCommand | 工具 → 整理 |
| OpenFileNameFixerWindowCommand | 工具 → 整理 |
| PushCommand | 插件库底部；操作菜单 |
| CheckCommand | 插件库底部；操作菜单 |
| ClearCachesCommand | 工具 → 文件清理；操作菜单 |
| RandomlySelectCommand | 插件库操作菜单 |
| RandomlySelectForSelectedItemsCommand | 插件库操作菜单（选中项） |
| DeleteRedundantVpkFilesCommand | 插件库操作菜单 |
| DeleteRedundantVpkFilesForSelectedItemsCommand | 插件库操作菜单（选中项） |
| ExportSelectedItemsAsAddonRootFile | 插件库操作菜单 |
| DeleteAddonsWithMissingFilesCommand | 插件库操作菜单 |
| DeleteAddonsWithMissingFilesForSelectedItemsCommand | 插件库操作菜单（选中项） |
| OpenAboutWindowCommand | 帮助页 |
| CheckUpdateCommand | 帮助页 |
| RunGameCommand | 插件库命令栏；操作菜单 |

命令仍绑定原 VM，目录/选择/执行中等 CanExecute 逻辑及危险操作确认沿用。新布局的键盘、焦点、溢出与实际可达性须实机复验。

## 逐窗覆盖清单

| 视图 | 本轮处理 | 待点验 |
|---|---|---|
| MainWindow | 侧栏、任务页、标题说明、命令分层、空态、上下文保留 | 切页/目录失败/命令禁用/窄窗状态区 |
| AppSettingsWindow | 分类导航及 Toolkit 设置控件；另列设置映射 | 每项编辑/依赖/校验/保存/关闭重开 |
| AddonNodeExplorerView | 搜索清除/筛选集中；排序视图飞出；详情小于 920 DIP 时折叠覆盖；保留原 ListView | 快捷键/右键/多选/拖拽/返回/焦点/滚动 |
| AddonNodeView、SectionDecorator | 基础属性卡片、标签、依赖、操作渐进展开、高级项；共享间距 | 各节点类型/空值/问题/动态工坊段 |
| AddonTagManagerWindow、AddonTagEditorWindow | 页头、统一留白；保留原多选及命令 | 添加/删除/改名/自动补全旧遗留 |
| AddonProblemListWindow | 页头、语义状态色、卡片、两个有限高度 ListView | 切换问题/插件页、长展开、检查刷新 |
| VpkAddonConflictListWindow | 页头、卡片、两个有限高度 ListView | 两页切换、刷新、长文件列表、详情展开 |
| DownloadItemListWindow/View | 页头、说明、有限高度列表宿主；修构造初始化 | 暂停/恢复/取消/多窗及共享队列 |
| AddonNameAutoSetterWindow | 页头和留白，保留目标/任务双区 | 窄窗命令、任务重试/应用/错误 |
| FileNameFixerWindow | 页头、卡片、虚拟化 ListView | 长路径/多条结果/成功失败 |
| NewWorkshopCollectionWindow | 页头、表单卡片、ToggleSwitch、创建主按钮与进度 | 输入/包含链接/创建失败/取消关闭 |
| AddonImportResultWindow | 页头与留白；保留可选择长文本及滚动 | 长结果与关闭 |
| AddonNodeCustomizeImageWindow | 页头、路径卡片、选择命令、说明 | 选择/清除/无效图片/保存 |
| AddonNodePickerWindow | 页头、留白；保留无详情 explorer 与确认 | 选择结果、取消、独立快捷键 |
| FileCleanerWindow | 页头、留白；保留有限高度结果与批量命令 | 全局/目录扫描、保留/删除/取消 |
| ReplacementOverviewWindow | 页头、留白；保留分组、筛选与定位 | 长列表/窄窗列/定位 |
| WorkshopVpkFinderWindow | 页头、有限高度虚拟化 ListView，移除外层滚动 | 标题获取/移除/扫描/创建 |
| OperationsProgressWindow | 页头、留白；保留进度与确认 | 进度/取消/成功失败/关闭 |
| AboutWindow | 统一留白与有限窗口内滚动；保留版本、框架、许可与联系信息 | 窄窗长文本与链接 |
| DialogService | 已审阅：原生 ContentDialog、单实例串行、明确按钮、异常文本可选择且有限高度滚动 | 模态归属、取消、长异常、再次打开 |

ListView 转换只更换最外层数据宿主，嵌套展开内容仍沿用原 ItemsControl；不能据此宣称所有层级全部虚拟化。替换总览与名称目标区仍需大数据性能实测。

## 公共规范与验证边界

页面留白 24 DIP；卡片内边距 16 DIP、描边 1 DIP、圆角 8 DIP；标题/正文/次级说明用 WinUI 样式；背景、状态和描边使用系统 ThemeResource。保留系统标题栏；未引入自定义拖动区或材质要求，实体主题背景为当前实现。

新文案使用 NativeUiTexts 的中英文 resx，沿用 Texts.Culture。工具窗标题尽量复用现有 Texts。

真实截图、浅深色/高对比度、100/150/200% 缩放、窄窗和兼容系统尚未验证。Computer Use 关闭导致无法点验；构建和测试不能替代这些证据。详见阶段记录。
