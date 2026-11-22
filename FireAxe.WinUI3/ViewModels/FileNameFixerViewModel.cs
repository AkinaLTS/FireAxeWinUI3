// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\FileNameFixerViewModel.cs
// ReactiveUI (WhenAnyValue + Subscribe, RaiseAndSetIfChanged, ReactiveCommand, IActivatableViewModel/WhenActivated,
// ViewModelActivator) -> ViewModelBase + [ObservableProperty] (with [NotifyPropertyChangedFor]) +
// [RelayCommand] + IViewModelLifecycle. No dialogs in this ViewModel (failures are kept in FixException).
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;

namespace FireAxe.ViewModels;

public partial class FileNameFixerViewModel : ViewModelBase, IViewModelLifecycle, IValidity
{
    public partial class Item : ViewModelBase
    {
        private readonly FileNameFixerViewModel _host;

        internal Item(FileNameFixerViewModel host, string filePath, string newFilePath)
        {
            _host = host;
            FilePath = filePath;
            NewFilePath = newFilePath;
        }

        public string FilePath { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RelativeNewFilePath))]
        public partial string NewFilePath { get; private set; }

        public string RelativeFilePath => Path.GetRelativePath(_host.AddonRoot.DirectoryPath, FilePath);

        public string RelativeNewFilePath => Path.GetRelativePath(_host.AddonRoot.DirectoryPath, NewFilePath);

        [ObservableProperty]
        public partial bool IsFixed { get; private set; }

        [ObservableProperty]
        public partial Exception? FixException { get; private set; }

        /// <summary>
        /// Replaces <c>this.WhenAnyValue(x =&gt; x.IsFixed).Subscribe(isFixed =&gt; ...)</c>.
        /// </summary>
        partial void OnIsFixedChanged(bool value)
        {
            if (value)
            {
                _host.NotifyItemFixed(this);
            }
        }

        public void Fix()
        {
            if (IsFixed)
            {
                return;
            }

            FixException = null;
            try
            {
                NewFilePath = FileSystemUtils.GetUniquePath(NewFilePath);
                FileSystemUtils.Move(FilePath, NewFilePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during fixing the file name: {FilePath}", FilePath);
                FixException = ex;
                return;
            }

            IsFixed = true;
        }
    }

    private readonly Item[] _items;

    private bool _isActive = false;

    private IDisposable? _invalidHandler = null;

    public FileNameFixerViewModel(AddonRoot addonRoot)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);

        AddonRoot = addonRoot;

        IsValid = true;
        IsAllFixed = true;

        _items = [.. FindItems()];

        UpdateIsAllFixed();
    }

    public AddonRoot AddonRoot { get; }

    [ObservableProperty]
    public partial bool IsValid { get; private set; }

    public IReadOnlyList<Item> Items => _items;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FixAllCommand))]
    public partial bool IsAllFixed { get; private set; }

    public void Activate()
    {
        if (_isActive)
        {
            return;
        }
        _isActive = true;

        _invalidHandler = AddonRoot.RegisterInvalidHandler(() => IsValid = false);
        if (!IsValid)
        {
            return;
        }
    }

    public void Deactivate()
    {
        if (!_isActive)
        {
            return;
        }
        _isActive = false;

        Utils.DisposeAndSetNull(ref _invalidHandler);
    }

    private bool CanFixAll() => !IsAllFixed;

    [RelayCommand(CanExecute = nameof(CanFixAll))]
    private void FixAll()
    {
        foreach (var item in _items)
        {
            item.Fix();
        }
    }

    private IEnumerable<Item> FindItems()
    {
        foreach (var path in AddonRoot.EnumerateUserFileSystemEntries())
        {
            var nameNoExt = Path.GetFileNameWithoutExtension(path);
            var newNameNoExt = AddonNode.SanitizeName(nameNoExt);
            if (newNameNoExt != nameNoExt)
            {
                var dirPath = Path.GetDirectoryName(path) ?? throw new IOException("Failed to get the directory path.");
                var ext = Path.GetExtension(path);
                var newPath = Path.Join(dirPath, newNameNoExt + ext);
                yield return new Item(this, path, newPath);
            }
        }
    }

    private void NotifyItemFixed(Item item)
    {
        UpdateIsAllFixed();
    }

    private void UpdateIsAllFixed()
    {
        bool allFixed = true;
        foreach (var item in _items)
        {
            if (!item.IsFixed)
            {
                allFixed = false;
                break;
            }
        }
        IsAllFixed = allFixed;
    }
}
