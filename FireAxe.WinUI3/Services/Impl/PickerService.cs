// Ported from FireAxe_Alt\FireAxe.GUI\Views\CommonMessageBoxes.cs (ChooseDirectory / ChooseFile / SaveFile)
// Avalonia IStorageProvider (OpenFolderPickerAsync / OpenFilePickerAsync / SaveFilePickerAsync)
//   -> Microsoft.Windows.Storage.Pickers (WinAppSDK 1.8+; takes the AppWindow id, no HWND needed — see P0 §S3).
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage.Pickers;

namespace FireAxe.Services;

/// <summary>
/// <see cref="IPickerService"/> over <c>Microsoft.Windows.Storage.Pickers</c>.
/// </summary>
public sealed class PickerService : IPickerService
{
    private readonly IActiveWindowProvider _activeWindowProvider;

    public PickerService(IActiveWindowProvider activeWindowProvider)
    {
        ArgumentNullException.ThrowIfNull(activeWindowProvider);
        _activeWindowProvider = activeWindowProvider;
    }

    public async System.Threading.Tasks.Task<string?> PickFolderAsync(FolderPickerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var picker = new FolderPicker(GetWindowId())
        {
            Title = options.Title ?? "",
            SuggestedFolder = GetSuggestedFolder(options.StartDirectoryPath),
        };
        var result = await picker.PickSingleFolderAsync();
        return string.IsNullOrEmpty(result?.Path) ? null : result.Path;
    }

    public async System.Threading.Tasks.Task<string?> PickFileAsync(FilePickerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var picker = new FileOpenPicker(GetWindowId())
        {
            Title = options.Title ?? "",
            SuggestedFolder = GetSuggestedFolder(options.StartDirectoryPath),
        };
        AddFileTypeFilter(picker.FileTypeFilter, options.FilePatterns);

        var result = await picker.PickSingleFileAsync();
        return string.IsNullOrEmpty(result?.Path) ? null : result.Path;
    }

    public async System.Threading.Tasks.Task<string?> SaveFileAsync(SaveFilePickerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var picker = new FileSavePicker(GetWindowId())
        {
            Title = options.Title ?? "",
            SuggestedFolder = GetSuggestedFolder(options.StartDirectoryPath),
            SuggestedFileName = options.SuggestedFileName ?? "",
            DefaultFileExtension = options.DefaultFileExtension ?? "",
        };

        // FileTypeChoices is <name, extensions>; the Avalonia version passed unnamed
        // FilePickerFileTypes, so the pattern itself is used as the display name.
        if (options.FilePatterns is { Count: > 0 } patterns)
        {
            foreach (var pattern in patterns)
            {
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    continue;
                }
                picker.FileTypeChoices.Add(pattern, [ToExtension(pattern)]);
            }
        }

        var result = await picker.PickSaveFileAsync();
        return string.IsNullOrEmpty(result?.Path) ? null : result.Path;
    }

    private static void AddFileTypeFilter(IList<string> fileTypeFilter, IReadOnlyList<string>? patterns)
    {
        bool added = false;
        if (patterns is not null)
        {
            foreach (var pattern in patterns)
            {
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    continue;
                }
                fileTypeFilter.Add(ToExtension(pattern));
                added = true;
            }
        }

        if (!added)
        {
            // WinUI requires at least one entry; "*" means "all files".
            fileTypeFilter.Add("*");
        }
    }

    /// <summary>
    /// The Avalonia options use globs (<c>*.addonroot</c>); the WinAppSDK pickers use extensions (<c>.addonroot</c>).
    /// </summary>
    private static string ToExtension(string pattern)
    {
        var trimmed = pattern.Trim();
        if (trimmed.StartsWith("*.", StringComparison.Ordinal))
        {
            return trimmed[1..];
        }
        if (trimmed.StartsWith('*'))
        {
            return trimmed[1..];
        }
        return trimmed;
    }

    private static string GetSuggestedFolder(string? startDirectoryPath)
    {
        if (!string.IsNullOrWhiteSpace(startDirectoryPath) && Directory.Exists(startDirectoryPath))
        {
            return startDirectoryPath;
        }
        return "";
    }

    private Microsoft.UI.WindowId GetWindowId()
    {
        if (_activeWindowProvider.ActiveWindow is not { } window)
        {
            throw new InvalidOperationException("There is no active window to attach the picker to.");
        }

        return window.AppWindow.Id;
    }
}
