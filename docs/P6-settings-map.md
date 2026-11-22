# P6.3 — 设置窗口：旧设置项 → 新分类映射

本文件是 P6.3（设置页分类 + Windows 11 风格卡片化）的落地对照表：`Views/AppSettingsWindow.xaml`
从"一个 ScrollViewer 顺序堆叠"重写为"左侧分类导航 + SettingsCard / SettingsExpander"。
**每一个原有设置项都在新布局中有明确去向**，绑定路径与数值提交语义保持不变。

- 窗口：`Views/AppSettingsWindow.xaml` / `AppSettingsWindow.xaml.cs`
- ViewModel（未改动）：`ViewModels/AppSettingsViewModel.cs`；设置模型（未改动）：`AppSettings.cs`
- 卡片组件：`CommunityToolkit.WinUI.Controls.SettingsControls 8.2.251219`（`SettingsCard` / `SettingsExpander`）
- 新增文案：`Resources/NativeUiTexts.resx` / `.zh-Hans.resx` / `NativeUiTexts.cs`（各 58 键，XAML 与三文件一一对应）

## 1. 分类导航（NavigationView）

| Tag | 导航标题（`NativeUiTexts`） | Icon | 页面元素 `x:Name` |
| --- | --- | --- | --- |
| `appearance` | `CategoryAppearance`（外观与语言） | FontColor | `AppearancePage`（默认选中） |
| `game` | `CategoryGame`（游戏） | Play | `GamePage` |
| `workshop` | `CategoryWorkshopAndDownload`（工坊与下载） | Download | `WorkshopPage` |
| `conflict` | `CategoryConflict`（冲突检查） | Important | `ConflictPage` |
| `backup` | `CategoryBackup`（备份） | Save | `BackupPage` |
| `advanced` | `CategoryAdvanced`（网络与高级） | Setting | `AdvancedPage` |
| `files` | `CategoryFiles`（文件位置） | OpenFile | `FilesPage` |

窄窗自适应：`PaneDisplayMode="Auto"` + `CompactModeThresholdWidth="700"` + `ExpandedModeThresholdWidth="1000"`
+ `OpenPaneLength="248"`；`IsSettingsVisible="False"`、`IsBackButtonVisible="Collapsed"`、
`AlwaysShowHeader="False"`，`PaneHeader` 显示 `NativeUiTexts.SettingsCategoryNavigation`。
页面切换在 `OnCategorySelectionChanged` 中按 `Tag` 设置各页 `Visibility`（构造期以
`AppearancePage is null` 提前返回，和主壳 `MainWindow` 的写法一致）。

## 2. 旧设置项 → 新位置

旧布局的 9 个分区（设置文件、用户界面、更新检查、游戏信息、VPK 模组冲突、工坊、下载、Web 代理、
文件备份、进程文件自定义）全部覆盖，无遗漏项。

| 旧分区 | 旧控件（原绑定） | 新分类 | 新容器 | 新控件 |
| --- | --- | --- | --- | --- |
| 设置文件路径 | `TextBlock`+`SettingsPathNoteConverter`（`Settings.SettingsFilePath`）+ `Open` 按钮 → `ShowSettingsFileCommand` | 文件位置 | `SettingsCard`（`ContentAlignment=Vertical`） | 同一转换器文本 + `Open` 按钮 |
| 用户界面 | `Language` ComboBox（`Settings.Language`, TwoWay, `LanguageItemsSource` + `LanguageNativeNameConverter`） | 外观与语言 | `SettingsCard` | ComboBox（原绑定与 ItemTemplate 不变） |
| 用户界面 | `ColorTheme` ComboBox（`Settings.ColorTheme`, TwoWay, `ColorThemes` + `EnumDescriptionConverter`） | 外观与语言 | `SettingsCard` | ComboBox（原绑定不变） |
| 更新检查 | `IsAutoCheckUpdateEnabled` CheckBox | 外观与语言 | `SettingsCard` | `ToggleSwitch`（TwoWay） |
| 更新检查 | `UpdateCheckRepository` TextBox + `UpdateCheckRepositoryNote` | 外观与语言 | `SettingsCard`（Vertical） | TextBox + 说明文本 |
| 游戏信息 | `GamePath` TextBox + `Select`（`SelectGamePathCommand`）+ `Find`（`FindGamePathCommand`） | 游戏 | `SettingsCard`（Vertical） | 同三件套（Grid：星号列 + 两个按钮列） |
| 游戏信息 | `CustomLaunchOptions` TextBox + `CustomLaunchOptionsNote` | 游戏 | `SettingsCard`（Vertical，Description 即该 Note） | TextBox |
| VPK 模组冲突 | `IgnoreHalfLife2FilesForVpkAddonConflicts` CheckBox | 冲突检查 | `SettingsCard` | `ToggleSwitch` |
| VPK 模组冲突 | 忽略列表目录路径（`CustomVpkAddonConflictIgnoringFilesDirectoryPath` + `CustomVpkPathNoteConverter`）、`Open`（`OpenCustomVpkAddonConflictIgnoringFilesDirectory`）、`Refresh`、`RefreshingHint`（按 `Settings.IsLoadingCustomVpkAddonConflictIgnoringFiles` 显示） | 冲突检查 | `SettingsCard`（Vertical） | 路径文本（可选中）+ `Open` 按钮 + `Refresh` 按钮（`Click="OnRefreshConflictIgnoringFilesClick"`）+ 刷新提示 |
| 工坊 | `IsAutoUpdateWorkshopItem` CheckBox | 工坊与下载 | `SettingsCard` | `ToggleSwitch` |
| 工坊 | `IsAutoDetectWorkshopItemLinkInClipboard` CheckBox | 工坊与下载 | `SettingsCard` | `ToggleSwitch` |
| 下载 | `IsAutoRedownload` CheckBox | 工坊与下载 | `SettingsCard` | `ToggleSwitch` |
| 下载 | `IsParallelDownloadEnabled` CheckBox | 工坊与下载 | `SettingsExpander`（`Content`） | `ToggleSwitch` |
| 下载 | `DownloadChunkCount`（`x:Name="downloadChunkCount"`，`{Binding DownloadChunkCount}`） | 工坊与下载 | `SettingsExpander.Items` → `SettingsCard`（父开关为真时可见） | `EditableTextBlock`（名称不变） |
| 下载 | `MaxConcurrentDownloads`（`x:Name="maxConcurrentDownloads"`，`{Binding MaxConcurrentDownloads}`） | 工坊与下载 | `SettingsExpander.Items` → `SettingsCard` | `EditableTextBlock`（名称不变） |
| 下载 | `DownloadSpeedNote` | 工坊与下载 | 并行下载 `SettingsExpander.Description` | 文本 |
| Web 代理 | `WebProxyAddress`（Value/Display 双绑定）+ `NeedRestartHint` | 网络与高级 | `SettingsCard`（`Description` = 需重启提示） | `TextBox`（`TwoWay` + `LostFocus` 提交，URI 非法时回滚并提示） |
| Web 代理 | `WebProxyCredentialUserName`（Value/Display） | 网络与高级 | `SettingsCard` | `TextBox`（`TwoWay` + `LostFocus` 提交） |
| Web 代理 | `WebProxyCredentialPassword`（Value/`Display`=`Texts.Hidden`） | 网络与高级 | `SettingsCard` | `PasswordBox`（`TwoWay`，`Password` 属性即时回写） |
| 文件备份 | `IsFileBackupEnabled` CheckBox | 备份 | `SettingsExpander`（`Content`） | `ToggleSwitch` |
| 文件备份 | `MaxRetainedBackupFileCount`（`x:Name="maxRetainedBackupFileCount"`，`{Binding MaxRetainedBackupFileCount}`） | 备份 | `SettingsExpander.Items` → `SettingsCard`（开关为真时可见） | `EditableTextBlock`（名称不变） |
| 文件备份 | `FileBackupIntervalMinutes`（`x:Name="fileBackupIntervalMinutes"`，`{Binding FileBackupIntervalMinutes}`） | 备份 | `SettingsExpander.Items` → `SettingsCard`（开关为真时可见） | `EditableTextBlock`（名称不变） |
| 文件备份 | `AddonRootBackupDirectoryLocationNote` + `Open` 按钮 → `OpenBackupDirectoryCommand` | 备份 | `SettingsExpander.Items` → `SettingsCard` | 说明文本 + `Open` 按钮（命令的 `CanExecute` 仍由 `AddonRootNotNull` 控制） |
| 进程文件自定义 | 打开目录：`OpenDirectoryCustomProcessFileName`（Value/Display）、`OpenDirectoryCustomProcessArguments`（Value/Display + `OpenDirectoryCustomProcessArgumentsWatermark`） | 网络与高级（"进程文件自定义"小节） | `SettingsExpander`（`Header=Texts.OpenDirectory`）→ 两个 `SettingsCard` | `TextBox` ×2（`TwoWay` + `PlaceholderText` 挂原 watermark） |
| 进程文件自定义 | 显示文件：`ShowFileCustomProcessFileName`（Value/Display）、`ShowFileCustomProcessArguments`（Value/Display + `ShowFileCustomProcessArgumentsWatermark`） | 网络与高级（"进程文件自定义"小节） | `SettingsExpander`（`Header=Texts.ShowFile`）→ 两个 `SettingsCard` | `TextBox` ×2（`TwoWay` + `PlaceholderText` 挂原 watermark） |
| （新） | 冲突忽略目录读取位置 | 文件位置 | `SettingsCard`（Vertical） | 同一 `CustomVpkPathNoteConverter` 只读路径，便于"文件位置"页集中查看 |

> 说明：冲突忽略列表目录在"冲突检查"页（带 Open/Refresh 操作）与"文件位置"页（只读路径）各出现一次，
> 属于同一数据源的两处只读/操作视图，不会产生两个可编辑入口。

## 3. 必须保持不变的契约

| 契约 | 说明 | 落地 |
| --- | --- | --- |
| 四个 `EditableTextBlock` 的 `x:Name` | `downloadChunkCount`、`maxConcurrentDownloads`、`maxRetainedBackupFileCount`、`fileBackupIntervalMinutes` | 新 XAML 逐字保留，`AppSettingsWindow.xaml.cs` 构造函数的四行 `Submitting +=` 未改 |
| 数值提交路径 | 提交时**直接赋值** VM 的 `int` 代理属性；`int.TryParse` 失败抛 `ArgumentException(Texts.ValueMustBeInteger)`；越界由 `AppSettings` 自己 `Math.Clamp(1,16)` 或抛 `ArgumentOutOfRangeException` | `CommitNumericValue(Action commit, e)` 未改提交动作，只把 catch 到的异常文案改为 `ObjectExplanationManager.Default.Get(ex, ExceptionExplanationScene.Input)`，使越界显示本地化的 `Texts.ValueMustBeWithinValidRange` 而非原始 .NET 英文消息 |
| 字符串提交路径 | 原 7 个字符串项用 `EditableTextBlock` 的 `Value`/`Display` 双绑定，而该控件**只通过 `Submitting` 事件回写**，没有 handler 时点击 ✓ 仍会写回，但重写成卡片后极易退化成只读（OneWay） | 改为标准控件：6 个 `TextBox` 两向绑定（`WebProxyAddress`/`UserName` 在 `LostFocus` 中 `GetBindingExpression(...).UpdateSource()` 提交，非法 URI 时 `UpdateTarget()` 回滚并用 `ObjectExplanationManager` 弹本地化提示）+ 1 个 `PasswordBox`（`Password` 属性两向绑定即时回写）。四个数值项仍用 `EditableTextBlock`，`Submitting` 契约不变 |
| 依赖可见性 | 分片数仅在"并行分片下载"开启时可用；备份保留个数/间隔仅在"文件备份"开启时可用 | 仍用 `VisibleWhenTrueConverter` 绑定 `Settings.IsParallelDownloadEnabled` / `Settings.IsFileBackupEnabled`（隐藏而非禁用，与原行为一致） |
| 刷新按钮 | 原 XAML 绑定的 `Settings.LoadCustomVpkAddonConflictIgnoringFilesCommand` 在 P1 之后已不存在（陈旧绑定，点击无反应） | 改为 `Click="OnRefreshConflictIgnoringFilesClick"` → `_ = ViewModel.Settings.LoadCustomVpkAddonConflictIgnoringFilesAsync();`（该方法自带重入保护并驱动 `IsLoadingCustomVpkAddonConflictIgnoringFiles`） |
| 需重启提示 | Web 代理三项修改后需重启 | `SettingsCard.Description` 显示 `Texts.NeedRestartHint`（地址、用户名）与 `NativeUiTexts.WebProxyPasswordDescription`（密码明文存储说明） |
| 保存语义 | 每次修改立即保存（`AppSettings.RequestSave`），无"确定/应用"按钮 | 未新增任何保存按钮；页脚 `NativeUiTexts.SettingsPageDescription` 仅作提示 |
| 主题 | 窗口 `RequestedTheme` 由 `WinUiAppThemeService` 按 `AppColorTheme` 设置（Default = 跟随系统） | 全部配色使用 `ThemeResource`（`ApplicationPageBackgroundThemeBrush`、`NativeCardStyle`、`TextFillColorSecondaryBrush` 等），`SettingsCard` 自身样式同样使用 ThemeResource，切主题即时跟随 |

## 4. 新增/复用的文案

- 复用 `Texts`（业务标签，未新增）：`Language`、`ColorTheme`、`IsAutoCheckUpdateEnabled`、`UpdateCheckRepository(+Note)`、
  `GamePath`、`Select`、`Find`、`CustomLaunchOptions(+Note)`、`VpkAddonConflict`、`IgnoreHalfLife2FilesForVpkAddonConflicts`、
  `CustomizeVpkAddonConflictIgnoringFilesNote`、`Open`、`Refresh`、`RefreshingHint`、`WorkshopItemAutoUpdate`、
  `AutoDetectWorkshopItemLinkInClipboard`、`AutoRedownload`、`IsParallelDownloadEnabled`、`DownloadChunkCount`、
  `MaxConcurrentDownloads`、`DownloadSpeedNote`、`WebProxy(+Address/CredentialUserName/CredentialPassword)`、`Hidden`、
  `NeedRestartHint`、`FileBackup`、`MaxRetainedBackupFileCount`、`FileBackupIntervalMinutes`、
  `AddonRootBackupDirectoryLocationNote`、`ProcessFileCustomization`、`OpenDirectory`、`ShowFile`、`ProcessFileName`、
  `ProcessArguments`、`OpenDirectoryCustomProcessArgumentsWatermark`、`ShowFileCustomProcessArgumentsWatermark`、`Settings`。
- 新增 `NativeUiTexts`（58 键）：分类标题与描述、各卡片描述、数值范围说明、需重启徽标等。
  `NativeUiTexts.resx` 为中性（英文），`NativeUiTexts.zh-Hans.resx` 为中文；两者键集与 `NativeUiTexts.cs`
  静态属性一一对应（已脚本核对：`resx` 键集 == `cs` 属性集，XAML 引用的属性 0 缺失）。
