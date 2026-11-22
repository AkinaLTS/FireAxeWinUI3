// P6 presentation resources; business labels remain in Texts (ported from FireAxe_Alt).
using System.Resources;

namespace FireAxe.Resources;

public static class NativeUiTexts
{
    private static readonly ResourceManager Manager = new("FireAxe.Resources.NativeUiTexts", typeof(NativeUiTexts).Assembly);
    private static string Get(string key) => Manager.GetString(key, Texts.Culture) ?? key;
    public static string PushPermissionHelp => Get(nameof(PushPermissionHelp));
    public static string Library => Get(nameof(Library));
    public static string LibraryDescription => Get(nameof(LibraryDescription));
    public static string ToolsDescription => Get(nameof(ToolsDescription));
    public static string DownloadsDescription => Get(nameof(DownloadsDescription));
    public static string HelpDescription => Get(nameof(HelpDescription));
    public static string Inspection => Get(nameof(Inspection));
    public static string Maintenance => Get(nameof(Maintenance));
    public static string Details => Get(nameof(Details));
    public static string SelectionHint => Get(nameof(SelectionHint));
    public static string ClearFilters => Get(nameof(ClearFilters));
    public static string SortAndView => Get(nameof(SortAndView));
    public static string OpenLibraryForTools => Get(nameof(OpenLibraryForTools));
    public static string Review => Get(nameof(Review));
    public static string Organize => Get(nameof(Organize));
    public static string CleanFiles => Get(nameof(CleanFiles));
    public static string DownloadHint => Get(nameof(DownloadHint));
    public static string QuickFind => Get(nameof(QuickFind));
    public static string CategoryAppearance => Get(nameof(CategoryAppearance));
    public static string CategoryGame => Get(nameof(CategoryGame));
    public static string CategoryWorkshopAndDownload => Get(nameof(CategoryWorkshopAndDownload));
    public static string CategoryConflict => Get(nameof(CategoryConflict));
    public static string CategoryBackup => Get(nameof(CategoryBackup));
    public static string CategoryAdvanced => Get(nameof(CategoryAdvanced));
    public static string CategoryFiles => Get(nameof(CategoryFiles));
    public static string SettingsCategoryNavigation => Get(nameof(SettingsCategoryNavigation));
    public static string SettingsPageDescription => Get(nameof(SettingsPageDescription));
    public static string AppearanceCategory => Get(nameof(AppearanceCategory));
    public static string AppearanceCategoryDescription => Get(nameof(AppearanceCategoryDescription));
    public static string GameCategory => Get(nameof(GameCategory));
    public static string GameCategoryDescription => Get(nameof(GameCategoryDescription));
    public static string WorkshopCategoryDescription => Get(nameof(WorkshopCategoryDescription));
    public static string ConflictCategory => Get(nameof(ConflictCategory));
    public static string ConflictCategoryDescription => Get(nameof(ConflictCategoryDescription));
    public static string BackupCategory => Get(nameof(BackupCategory));
    public static string BackupCategoryDescription => Get(nameof(BackupCategoryDescription));
    public static string AdvancedCategoryDescription => Get(nameof(AdvancedCategoryDescription));
    public static string FilesCategory => Get(nameof(FilesCategory));
    public static string FilesCategoryDescription => Get(nameof(FilesCategoryDescription));
    public static string LanguageDescription => Get(nameof(LanguageDescription));
    public static string ColorThemeDescription => Get(nameof(ColorThemeDescription));
    public static string UpdateCheckEnabledDescription => Get(nameof(UpdateCheckEnabledDescription));
    public static string UpdateCheckRepositoryDescription => Get(nameof(UpdateCheckRepositoryDescription));
    public static string GamePathDescription => Get(nameof(GamePathDescription));
    public static string SettingsFileDescription => Get(nameof(SettingsFileDescription));
    public static string NeedRestartBadge => Get(nameof(NeedRestartBadge));
    public static string DownloadChunkCountDescription => Get(nameof(DownloadChunkCountDescription));
    public static string MaxConcurrentDownloadsDescription => Get(nameof(MaxConcurrentDownloadsDescription));
    public static string FileBackupEnabledDescription => Get(nameof(FileBackupEnabledDescription));
    public static string FileBackupRetainedCountDescription => Get(nameof(FileBackupRetainedCountDescription));
    public static string FileBackupIntervalDescription => Get(nameof(FileBackupIntervalDescription));
    public static string AddonRootBackupOpenDescription => Get(nameof(AddonRootBackupOpenDescription));
    public static string WebProxyPasswordDescription => Get(nameof(WebProxyPasswordDescription));
    public static string WebProxyCategoryDescription => Get(nameof(WebProxyCategoryDescription));
    public static string ConflictIgnoringFilesDescription => Get(nameof(ConflictIgnoringFilesDescription));
    public static string ProcessFileCustomizationDescription => Get(nameof(ProcessFileCustomizationDescription));
    public static string ProcessFileNameDescription => Get(nameof(ProcessFileNameDescription));
    public static string ProcessArgumentsDescription => Get(nameof(ProcessArgumentsDescription));
}
