// Ported from FireAxe_Alt\FireAxe.GUI\Views\CommonMessageBoxes.cs (ChooseDirectory/ChooseFile/SaveFile)
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FireAxe.Services;

/// <summary>Base options for the file system pickers.</summary>
public class FileSystemPickerOptions
{
    public string? Title { get; set; } = null;

    public string? StartDirectoryPath { get; set; } = null;

    public string? SuggestedFileName { get; set; } = null;
}

/// <summary>Options for <see cref="IPickerService.PickFolderAsync"/>.</summary>
public sealed class FolderPickerOptions : FileSystemPickerOptions
{
}

/// <summary>Options for <see cref="IPickerService.PickFileAsync"/>.</summary>
public sealed class FilePickerOptions : FileSystemPickerOptions
{
    /// <summary>Glob patterns such as <c>*.addonroot</c>. Null means "any file".</summary>
    public IReadOnlyList<string>? FilePatterns { get; set; } = null;
}

/// <summary>Options for <see cref="IPickerService.SaveFileAsync"/>.</summary>
public sealed class SaveFilePickerOptions : FileSystemPickerOptions
{
    public IReadOnlyList<string>? FilePatterns { get; set; } = null;

    public string? DefaultFileExtension { get; set; } = null;
}

/// <summary>
/// Replaces the 5 choose-directory/choose-file/save-file <c>Interaction&lt;T,U&gt;</c> handlers.
/// Implemented over <c>Microsoft.Windows.Storage.Pickers</c> (P0 §S3: needs only <c>AppWindow.Id</c>, no HWND).
/// </summary>
public interface IPickerService
{
    /// <summary>Returns the picked folder path, or null when cancelled.</summary>
    Task<string?> PickFolderAsync(FolderPickerOptions options);

    /// <summary>Returns the picked file path, or null when cancelled.</summary>
    Task<string?> PickFileAsync(FilePickerOptions options);

    /// <summary>Returns the chosen save path, or null when cancelled.</summary>
    Task<string?> SaveFileAsync(SaveFilePickerOptions options);
}
