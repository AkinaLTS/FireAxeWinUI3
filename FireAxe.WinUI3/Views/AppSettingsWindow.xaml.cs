using System;
using System.Globalization;
using FireAxe.Resources;
using FireAxe.ViewModels;
using FireAxe.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace FireAxe.Views;

public sealed partial class AppSettingsWindow : HostedPage
{
    public AppSettingsWindow(AppSettingsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        InitializeComponent();
        ((FrameworkElement)Content).DataContext = viewModel;
        _ = new ViewModelLifecycleBinder((FrameworkElement)Content);

        downloadChunkCount.Submitting += OnDownloadChunkCountSubmitting;
        maxConcurrentDownloads.Submitting += OnMaxConcurrentDownloadsSubmitting;
        maxRetainedBackupFileCount.Submitting += OnMaxRetainedBackupFileCountSubmitting;
        fileBackupIntervalMinutes.Submitting += OnFileBackupIntervalMinutesSubmitting;
    }

    private void OnLanguageLabelLoaded(object sender, RoutedEventArgs e)
    {
        // WinUI skips the converter for a null item; display the existing default label.
        if (sender is TextBlock label && label.DataContext is null)
            label.Text = Texts.Default;
    }

    public AppSettingsViewModel ViewModel { get; }

    public AppColorTheme[] ColorThemes { get; } = Enum.GetValues<AppColorTheme>();

    public string SelectedCategory { get; private set; } = "appearance";
    public event EventHandler? CategoryChanged;
    public void SelectCategory(string category)
    {
        foreach (var item in CategoryNavigation.MenuItems)
            if (item is NavigationViewItem nav && (string?)nav.Tag == category) CategoryNavigation.SelectedItem = nav;
    }
    private void OnCategorySelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        // Selection fires while InitializeComponent is still building the content.
        if (AppearancePage is null)
        {
            return;
        }

        string? category = (args.SelectedItemContainer as NavigationViewItem)?.Tag as string;
        AppearancePage.Visibility = VisibleWhen(category, "appearance");
        GamePage.Visibility = VisibleWhen(category, "game");
        WorkshopPage.Visibility = VisibleWhen(category, "workshop");
        ConflictPage.Visibility = VisibleWhen(category, "conflict");
        BackupPage.Visibility = VisibleWhen(category, "backup");
        AdvancedPage.Visibility = VisibleWhen(category, "advanced");
        FilesPage.Visibility = VisibleWhen(category, "files");
        SelectedCategory = category ?? "appearance";
        CategoryChanged?.Invoke(this, EventArgs.Empty);
    }

    private static Visibility VisibleWhen(string? category, string expected)
    {
        return category == expected ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Commit point for the free-form text settings that used to live in an <see cref="EditableTextBlock"/>.
    /// Native TextBox uses TwoWay bindings; explicitly flush pending edits on focus loss, and
    /// <see cref="TextBox.GetBindingExpression"/> + <c>UpdateSource</c> is the binding API that makes the
    /// TwoWay write happen without re-implementing the assignment here.
    /// </summary>
    private void OnStringSettingLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        BindingExpression? binding = textBox.GetBindingExpression(TextBox.TextProperty);
        try
        {
            binding?.UpdateSource();
        }
        catch (Exception ex)
        {
            // e.g. Settings.WebProxyAddress rejects a malformed URI (ArgumentException(Texts.InvalidUri)).
            // Revert the editor to the stored value and report the localized explanation as a tooltip.
            ToolTipService.SetToolTip(textBox, ObjectExplanationManager.Default.Get(ex, ExceptionExplanationScene.Input));
            if (binding is not null)
            {
                textBox.SetBinding(TextBox.TextProperty, binding.ParentBinding);
            }
            return;
        }

        ToolTipService.SetToolTip(textBox, null);
    }

    /// <summary>
    /// The previous layout bound <c>Settings.LoadCustomVpkAddonConflictIgnoringFilesCommand</c>, but
    /// <see cref="AppSettings.LoadCustomVpkAddonConflictIgnoringFilesAsync"/> is a plain method since P1
    /// (the ReactiveUI command was removed). Forward the click to that method; it guards re-entry and
    /// reports progress through <see cref="AppSettings.IsLoadingCustomVpkAddonConflictIgnoringFiles"/>.
    /// </summary>
    private void OnRefreshConflictIgnoringFilesClick(object sender, RoutedEventArgs e)
    {
        _ = ViewModel.Settings.LoadCustomVpkAddonConflictIgnoringFilesAsync();
    }

    private void OnDownloadChunkCountSubmitting(object? sender, EditableTextBlockSubmittingEventArgs e)
    {
        CommitNumericValue(() => ViewModel.DownloadChunkCount = e.NewValue, e);
    }

    private void OnMaxConcurrentDownloadsSubmitting(object? sender, EditableTextBlockSubmittingEventArgs e)
    {
        CommitNumericValue(() => ViewModel.MaxConcurrentDownloads = e.NewValue, e);
    }

    private void OnMaxRetainedBackupFileCountSubmitting(object? sender, EditableTextBlockSubmittingEventArgs e)
    {
        CommitNumericValue(() => ViewModel.MaxRetainedBackupFileCount = e.NewValue, e);
    }

    private void OnFileBackupIntervalMinutesSubmitting(object? sender, EditableTextBlockSubmittingEventArgs e)
    {
        CommitNumericValue(() => ViewModel.FileBackupIntervalMinutes = e.NewValue, e);
    }

    /// <summary>
    /// Commits a numeric editor the same way the ViewModel expects it (a plain assignment, so
    /// <see cref="AppSettings"/> keeps its clamping / range checks), and shows the failure inside the
    /// editor instead of losing it. The message goes through <see cref="ObjectExplanationManager"/> so
    /// <see cref="ArgumentOutOfRangeException"/> becomes the localized "value must be within the valid
    /// range" text rather than the raw .NET message (same explanation the ViewModel uses for inputs).
    /// </summary>
    private static void CommitNumericValue(Action commit, EditableTextBlockSubmittingEventArgs e)
    {
        try
        {
            commit();
        }
        catch (Exception ex)
        {
            e.ErrorMessage = ObjectExplanationManager.Default.Get(ex, ExceptionExplanationScene.Input);
        }
    }
}

public sealed class CustomVpkPathNoteConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return Texts.CustomizeVpkAddonConflictIgnoringFilesNote.FormatNoThrow(value as string ?? "");
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed class SettingsPathNoteConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return Texts.AppSettingsFileLocationNote.FormatNoThrow(value as string ?? "");
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed class LanguageNativeNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string cultureName)
        {
            return Texts.Default;
        }

        try
        {
            return new CultureInfo(cultureName).NativeName;
        }
        catch (CultureNotFoundException)
        {
            return cultureName;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
