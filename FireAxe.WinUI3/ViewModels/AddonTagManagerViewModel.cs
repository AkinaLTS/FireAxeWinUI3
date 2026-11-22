// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\AddonTagManagerViewModel.cs
// ReactiveUI -> CommunityToolkit.Mvvm + BCL events:
//   * IActivatableViewModel/WhenActivated            -> IViewModelLifecycle (Activate/Deactivate)
//   * Interaction<string, string?> / Interaction<Unit, ...> (5) -> IDialogService (texts/titles taken
//     verbatim from Views\AddonTagManagerWindow.axaml.cs, which is where they used to live)
//   * Rx canExecute observables (this.WhenAnyValue(...)) -> [RelayCommand(CanExecute = ...)] with an
//     explicit NotifyCanExecuteChanged when the backing state changes.
// The five public Interaction<,> members cannot exist without ReactiveUI and are gone; every other
// public member keeps its exact name/type.
using CommunityToolkit.Mvvm.Input;
using FireAxe.Resources;
using FireAxe.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading.Tasks;

namespace FireAxe.ViewModels;

public partial class AddonTagManagerViewModel : ViewModelBase, IViewModelLifecycle
{
    private readonly MainWindowViewModel _mainWindowViewModel;

    private bool _isActive = false;

    private bool _addonRootPresent = false;

    private readonly ObservableCollection<string> _selectedTags = new();
    private string? _selectedTag = null;
    private bool _hasSelection = false;
    private bool _hasSingleSelection = false;

    public AddonTagManagerViewModel(MainWindowViewModel mainWindowViewModel)
    {
        ArgumentNullException.ThrowIfNull(mainWindowViewModel);
        _mainWindowViewModel = mainWindowViewModel;

        ((INotifyCollectionChanged)_selectedTags).CollectionChanged += OnSelectedTagsCollectionChanged;
    }

    public MainWindowViewModel MainWindowViewModel => _mainWindowViewModel;

    public ObservableCollection<string> SelectedTags => _selectedTags;

    public string? SelectedTag
    {
        get => _selectedTag;
        private set => SetProperty(ref _selectedTag, value);
    }

    public bool HasSelection
    {
        get => _hasSelection;
        private set
        {
            if (SetProperty(ref _hasSelection, value))
            {
                DeleteCommand.NotifyCanExecuteChanged();
                DeleteCompletelyCommand.NotifyCanExecuteChanged();
                MoveUpCommand.NotifyCanExecuteChanged();
                MoveDownCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasSingleSelection
    {
        get => _hasSingleSelection;
        private set
        {
            if (SetProperty(ref _hasSingleSelection, value))
            {
                RenameCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Called by the View when it is attached (was ReactiveUI's <c>WhenActivated</c>).
    /// </summary>
    public void Activate()
    {
        if (_isActive)
        {
            return;
        }
        _isActive = true;

        _mainWindowViewModel.PropertyChanged += OnMainWindowViewModelPropertyChanged;

        // Rx's WhenAnyValue pushed the current value on subscription; reproduce that first emit.
        OnAddonRootChanged();
    }

    /// <summary>
    /// Called by the View when it is detached. Idempotent and re-entrant
    /// (Activate -> Deactivate -> Activate is legal).
    /// </summary>
    public void Deactivate()
    {
        if (!_isActive)
        {
            return;
        }
        _isActive = false;

        _mainWindowViewModel.PropertyChanged -= OnMainWindowViewModelPropertyChanged;

        IsAddonRootPresent = false;
    }

    /// <remarks>
    /// Also the body of <c>RefreshCommand</c> (was <c>ReactiveCommand.Create(Refresh, ...)</c>): the
    /// method keeps its public name, the relay command is generated from it.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanRefresh))]
    public void Refresh()
    {
        var addonRoot = _mainWindowViewModel.AddonRoot;
        if (addonRoot == null)
        {
            return;
        }

        addonRoot.RefreshCustomTags();
    }

    public IEnumerable<string> GetSelectedTagsInOriginalOrder()
    {
        var addonRoot = _mainWindowViewModel.AddonRoot;
        if (addonRoot == null)
        {
            yield break;
        }

        HashSet<string> selectedTagSet = [.. SelectedTags];
        foreach (var tag in addonRoot.CustomTags)
        {
            if (selectedTagSet.Contains(tag))
            {
                yield return tag;
            }
        }
    }

    private bool IsAddonRootPresent
    {
        get => _addonRootPresent;
        set
        {
            if (SetProperty(ref _addonRootPresent, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                AddCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private bool CanRefresh() => IsAddonRootPresent;

    private bool CanAdd() => IsAddonRootPresent;

    private bool CanRename() => HasSingleSelection;

    private bool CanOperateOnSelection() => HasSelection;

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private async Task AddAsync()
    {
        var addonRoot = _mainWindowViewModel.AddonRoot;
        if (addonRoot == null)
        {
            return;
        }

        string? input = "";
        while (true)
        {
            input = await InputTagNameAsync(input, Texts.NewTag);
            if (input == null)
            {
                return;
            }
            if (input.Length == 0)
            {
                await ShowInputCannotBeEmptyAsync();
                continue;
            }
            if (!addonRoot.IsValid)
            {
                return;
            }
            if (!addonRoot.AddCustomTag(input))
            {
                await ShowTagExistAsync();
                continue;
            }
            break;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRename))]
    private async Task RenameAsync()
    {
        var addonRoot = _mainWindowViewModel.AddonRoot;
        if (addonRoot == null)
        {
            return;
        }
        var oldTag = SelectedTag;
        if (oldTag == null)
        {
            return;
        }

        string? input = oldTag;
        while (true)
        {
            input = await InputTagNameAsync(input, Texts.RenameTag);
            if (input == null)
            {
                return;
            }
            if (input.Length == 0)
            {
                await ShowInputCannotBeEmptyAsync();
                continue;
            }
            if (input == oldTag)
            {
                return;
            }
            if (!addonRoot.IsValid)
            {
                return;
            }
            addonRoot.RenameCustomTag(oldTag, input);
            break;
        }
    }

    [RelayCommand(CanExecute = nameof(CanOperateOnSelection))]
    private void Delete()
    {
        var addonRoot = _mainWindowViewModel.AddonRoot;
        if (addonRoot == null)
        {
            return;
        }

        string[] tags = [.. SelectedTags];
        foreach (var tag in tags)
        {
            addonRoot.RemoveCustomTag(tag);
        }
    }

    [RelayCommand(CanExecute = nameof(CanOperateOnSelection))]
    private async Task DeleteCompletelyAsync()
    {
        bool confirm = await ConfirmDeleteTagCompletelyAsync();
        if (!confirm)
        {
            return;
        }

        var addonRoot = _mainWindowViewModel.AddonRoot;
        if (addonRoot == null)
        {
            return;
        }

        string[] tags = [.. SelectedTags];
        foreach (var tag in tags)
        {
            addonRoot.RemoveCustomTagCompletely(tag);
        }
    }

    [RelayCommand(CanExecute = nameof(CanOperateOnSelection))]
    private void MoveUp()
    {
        var addonRoot = _mainWindowViewModel.AddonRoot;
        if (addonRoot == null)
        {
            return;
        }

        string[] tags = [.. GetSelectedTagsInOriginalOrder()];
        foreach (var tag in tags)
        {
            int idx = addonRoot.CustomTags.IndexOf(tag);
            if (idx == -1)
            {
                continue;
            }
            if (idx == 0)
            {
                continue;
            }
            addonRoot.MoveCustomTag(idx, idx - 1);
        }

        _selectedTags.Clear();
        foreach (var tag in tags)
        {
            _selectedTags.Add(tag);
        }
    }

    [RelayCommand(CanExecute = nameof(CanOperateOnSelection))]
    private void MoveDown()
    {
        var addonRoot = _mainWindowViewModel.AddonRoot;
        if (addonRoot == null)
        {
            return;
        }

        string[] tags = [.. GetSelectedTagsInOriginalOrder()];
        Array.Reverse(tags);
        foreach (var tag in tags)
        {
            int idx = addonRoot.CustomTags.IndexOf(tag);
            if (idx == -1)
            {
                continue;
            }
            if (idx == addonRoot.CustomTags.Count - 1)
            {
                continue;
            }
            addonRoot.MoveCustomTag(idx, idx + 1);
        }

        _selectedTags.Clear();
        foreach (var tag in tags)
        {
            _selectedTags.Add(tag);
        }
    }

    private void OnSelectedTagsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_selectedTags.Count == 1)
        {
            SelectedTag = _selectedTags[0];
            HasSingleSelection = true;
        }
        else
        {
            SelectedTag = null;
            HasSingleSelection = false;
        }

        HasSelection = _selectedTags.Count > 0;
    }

    private void OnMainWindowViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.AddonRoot))
        {
            OnAddonRootChanged();
        }
    }

    private void OnAddonRootChanged()
    {
        IsAddonRootPresent = _mainWindowViewModel.AddonRoot != null;
        Refresh();
    }

    /// <summary>Was the handler of <c>AddTagInteraction</c> / <c>RenameTagInteraction</c>.</summary>
    private static async Task<string?> InputTagNameAsync(string? defaultValue, string title)
    {
        if (AppServices.TryGetService<IDialogService>() is not { } dialogService)
        {
            return null;
        }

        return await dialogService.InputTextAsync(Texts.InputTagNameMessage, title, defaultValue ?? "");
    }

    /// <summary>Was the handler of <c>ShowInputCannotBeEmptyInteraction</c>.</summary>
    private static async Task ShowInputCannotBeEmptyAsync()
    {
        if (AppServices.TryGetService<IDialogService>() is { } dialogService)
        {
            await dialogService.ShowInfoAsync(Texts.InputCannotBeEmptyMessage, Texts.Error);
        }
    }

    /// <summary>Was the handler of <c>ShowTagExistInteraction</c>.</summary>
    private static async Task ShowTagExistAsync()
    {
        if (AppServices.TryGetService<IDialogService>() is { } dialogService)
        {
            await dialogService.ShowInfoAsync(Texts.TagNameExistMessage, Texts.Error);
        }
    }

    /// <summary>Was the handler of <c>ConfirmDeleteTagCompletelyInteraction</c>.</summary>
    private static async Task<bool> ConfirmDeleteTagCompletelyAsync()
    {
        if (AppServices.TryGetService<IDialogService>() is not { } dialogService)
        {
            return false;
        }

        return await dialogService.ConfirmAsync(Texts.ConfirmDeleteTagCompletely, Texts.ConfirmDelete);
    }
}
