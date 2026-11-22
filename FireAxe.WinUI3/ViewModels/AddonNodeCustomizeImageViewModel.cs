// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\AddonNodeCustomizeImageViewModel.cs
using CommunityToolkit.Mvvm.Input;
using FireAxe.Resources;
using FireAxe.Services;
using Serilog;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;

namespace FireAxe.ViewModels;

public partial class AddonNodeCustomizeImageViewModel : ViewModelBase, IViewModelLifecycle
{
    /// <summary>Image patterns accepted by the picker (was <c>FilePickerFileTypes.ImageJpg/ImagePng</c>).</summary>
    private static readonly string[] s_imagePatterns = ["*.jpg", "*.jpeg", "*.png"];

    private readonly AddonNode _addon;

    private bool _isActive = false;

    public AddonNodeCustomizeImageViewModel(AddonNode addon)
    {
        ArgumentNullException.ThrowIfNull(addon);
        _addon = addon;
    }

    public AddonNode Addon => _addon;

    public string CustomImagePath
    {
        get => _addon.CustomImagePath ?? "";
        set
        {
            _addon.CustomImagePath = value.Length == 0 ? null : value;
        }
    }

    public void Activate()
    {
        if (_isActive)
        {
            return;
        }
        _isActive = true;

        _addon.PropertyChanged += OnAddonPropertyChanged;
    }

    public void Deactivate()
    {
        if (!_isActive)
        {
            return;
        }
        _isActive = false;

        _addon.PropertyChanged -= OnAddonPropertyChanged;
    }

    [RelayCommand]
    private async Task SelectCustomImagePathAsync()
    {
        var pickerService = AppServices.TryGetService<IPickerService>();
        var dialogService = AppServices.TryGetService<IDialogService>();
        if (pickerService is null)
        {
            return;
        }

        var addonRootDirectoryPath = _addon.Root.DirectoryPath;

        var path = await pickerService.PickFileAsync(new FilePickerOptions
        {
            StartDirectoryPath = addonRootDirectoryPath,
            FilePatterns = s_imagePatterns
        });
        if (path is null)
        {
            return;
        }

        string? relativePath = null;
        try
        {
            relativePath = FileSystemUtils.NormalizePath(Path.GetRelativePath(addonRootDirectoryPath, path));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during getting relative path of picked file: {FilePath}", path);
        }

        if (relativePath is null)
        {
            return;
        }

        try
        {
            CustomImagePath = relativePath;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during setting the custom image path: {FilePath}", relativePath);
            if (dialogService is not null)
            {
                await dialogService.ShowExceptionAsync(ex);
            }
        }
    }

    private void OnAddonPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AddonNode.CustomImagePath))
        {
            OnPropertyChanged(nameof(CustomImagePath));
        }
    }
}
