// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\AddonTagEditorViewModel.cs
// ReactiveUI -> CommunityToolkit.Mvvm + BCL events:
//   * IActivatableViewModel/WhenActivated      -> IViewModelLifecycle (Activate/Deactivate)
//   * Interaction<string, string?> / Interaction<Unit, Unit> (3) -> IDialogService
//   * Rx canExecute observables                -> [RelayCommand(CanExecute = ...)] + NotifyCanExecuteChanged
// See the note on AddAsync: the Avalonia view opened a dedicated AddAddonTagWindow (with the list of
// existing tags) for the tag input; IDialogService only offers a plain text input, so the existing-tag
// list is not reproduced.
using CommunityToolkit.Mvvm.Input;
using FireAxe.Resources;
using FireAxe.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;

namespace FireAxe.ViewModels;

public partial class AddonTagEditorViewModel : ViewModelBase, IViewModelLifecycle, IValidity
{
    private readonly AddonNode _addon;

    private bool _isActive = false;
    private IDisposable? _invalidHandler = null;

    private bool _isValid = true;

    private readonly ObservableCollection<string> _selectedTags = new();
    private bool _hasSelection = false;

    public AddonTagEditorViewModel(AddonNode addon)
    {
        ArgumentNullException.ThrowIfNull(addon);
        _addon = addon;

        ((INotifyCollectionChanged)_selectedTags).CollectionChanged += OnSelectedTagsCollectionChanged;
    }

    public AddonNode Addon => _addon;

    public bool IsValid
    {
        get => _isValid;
        private set => SetProperty(ref _isValid, value);
    }

    public ObservableCollection<string> SelectedTags => _selectedTags;

    public bool HasSelection
    {
        get => _hasSelection;
        private set
        {
            if (SetProperty(ref _hasSelection, value))
            {
                DeleteCommand.NotifyCanExecuteChanged();
                MoveUpCommand.NotifyCanExecuteChanged();
                MoveDownCommand.NotifyCanExecuteChanged();
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

        _invalidHandler = Addon.RegisterInvalidHandler(() => IsValid = false);
        if (!IsValid)
        {
            return;
        }
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

        Utils.DisposeAndSetNull(ref _invalidHandler);
    }

    public IEnumerable<string> GetSelectedTagsInOriginalOrder()
    {
        HashSet<string> selectedTags = [.. SelectedTags];
        foreach (var tag in _addon.Tags)
        {
            if (selectedTags.Contains(tag))
            {
                yield return tag;
            }
        }
    }

    private bool CanOperateOnSelection() => HasSelection;

    [RelayCommand]
    private async Task AddAsync()
    {
        string? input = "";
        while (true)
        {
            // Was the handler of AddTagInteraction, which opened AddAddonTagWindow
            // (title Texts.AddTag, prompt Texts.InputTagNameMessage, autocomplete over the existing
            // tags). IDialogService has no equivalent window-with-list, so this is a plain input box:
            // the strings are identical, the "pick an existing tag from the list" affordance is lost.
            input = await InputTagNameAsync(input);
            if (input == null)
            {
                return;
            }
            if (input.Length == 0)
            {
                await ShowInputCannotBeEmptyAsync();
                continue;
            }
            if (!_addon.AddTag(input))
            {
                await ShowTagExistAsync();
                continue;
            }
            break;
        }
    }

    [RelayCommand(CanExecute = nameof(CanOperateOnSelection))]
    private void Delete()
    {
        string[] tags = [.. SelectedTags];
        foreach (var tag in tags)
        {
            _addon.RemoveTag(tag);
        }
    }

    [RelayCommand(CanExecute = nameof(CanOperateOnSelection))]
    private void MoveUp()
    {
        string[] tags = [.. GetSelectedTagsInOriginalOrder()];
        foreach (var tag in tags)
        {
            int idx = _addon.Tags.IndexOf(tag);
            if (idx == -1)
            {
                continue;
            }
            if (idx == 0)
            {
                continue;
            }
            _addon.MoveTag(idx, idx - 1);
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
        string[] tags = [.. GetSelectedTagsInOriginalOrder()];
        Array.Reverse(tags);
        foreach (var tag in tags)
        {
            int idx = _addon.Tags.IndexOf(tag);
            if (idx == -1)
            {
                continue;
            }
            if (idx == _addon.Tags.Count - 1)
            {
                continue;
            }
            _addon.MoveTag(idx, idx + 1);
        }

        _selectedTags.Clear();
        foreach (var tag in tags)
        {
            _selectedTags.Add(tag);
        }
    }

    private void OnSelectedTagsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HasSelection = _selectedTags.Count > 0;
    }

    /// <summary>Was the body of the <c>AddTagInteraction</c> handler in AddonTagEditorWindow.axaml.cs.</summary>
    private static async Task<string?> InputTagNameAsync(string? defaultValue)
    {
        if (AppServices.TryGetService<IDialogService>() is not { } dialogService)
        {
            return null;
        }

        return await dialogService.InputTextAsync(Texts.InputTagNameMessage, Texts.AddTag, defaultValue ?? "");
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
}
