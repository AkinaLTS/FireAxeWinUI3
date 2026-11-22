// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\DownloadItemListViewModel.cs
// Mechanisms replaced:
//   DynamicData (ToObservableChangeSet().Transform(...).Bind(out ...)) -> one mirrored ObservableCollection<DownloadItemViewModel>
//     wrapped by a ReadOnlyObservableCollection (kept incremental: same Add/Remove/Reset correspondence, stable item identity)
//   ObservableAsPropertyHelper<bool> _hasSelection (WhenAnyValue(Selection).Select(...).ToProperty) -> computed property + [NotifyPropertyChangedFor]
//   Avalonia DispatcherTimer.Run (clean timer, started in the constructor) -> IUiDispatcher.StartTimer (same 0.5 s interval)
//   ReactiveUI WhenActivated(Disposable.Create(() => Selection = null)) -> IViewModelLifecycle.Deactivate
//   Commands bound from XAML as methods (Pause/Resume/Cancel) -> [RelayCommand] (members and names kept)
//   ViewModelActivator Activator -> dropped (ReactiveUI type; same as the other ported ViewModels)
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FireAxe.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace FireAxe.ViewModels;

public partial class DownloadItemListViewModel : ViewModelBase, IViewModelLifecycle, IDisposable
{
    private static readonly TimeSpan CleanInterval = TimeSpan.FromSeconds(0.5);

    private bool _disposed = false;
    private bool _isActive = false;

    private readonly ObservableCollection<IDownloadItem> _downloadItems = new();
    private readonly ObservableCollection<DownloadItemViewModel> _downloadItemViewModels = new();
    private readonly ReadOnlyObservableCollection<DownloadItemViewModel> _downloadItemViewModelsReadOnly;

    /// <summary>Handle of the periodic clean timer (was the <c>DispatcherTimer</c> disposed in <see cref="Dispose"/>).</summary>
    private IDisposable? _cleanTimer = null;

    public DownloadItemListViewModel()
    {
        _downloadItemViewModelsReadOnly = new(_downloadItemViewModels);

        _downloadItems.CollectionChanged += OnDownloadItemsCollectionChanged;

        _cleanTimer = AppServices.TryGetService<IUiDispatcher>()?.StartTimer(CleanInterval, () =>
        {
            Clean();
            return true;
        });
    }

    public ReadOnlyObservableCollection<DownloadItemViewModel> DownloadItemViewModels => _downloadItemViewModelsReadOnly;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial IReadOnlyList<DownloadItemViewModel>? Selection { get; set; }

    public IEnumerable<IDownloadItem> SelectedDownloadItems
    {
        get
        {
            var selection = Selection;
            if (selection == null)
            {
                yield break;
            }
            foreach (var item in selection)
            {
                yield return item.DownloadItem;
            }
        }
    }

    public bool HasSelection => Selection is { Count: > 0 };

    /// <summary>
    /// Called by the View when it is attached to the visual tree
    /// (was ReactiveUI's <c>WhenActivated</c>).
    /// </summary>
    public void Activate()
    {
        if (_isActive || _disposed)
        {
            return;
        }
        _isActive = true;
    }

    /// <summary>
    /// Called by the View when it is detached. Drops the selection
    /// (was the <c>Disposable.Create(() =&gt; Selection = null)</c> registered in <c>WhenActivated</c>).
    /// </summary>
    public void Deactivate()
    {
        if (!_isActive)
        {
            return;
        }
        _isActive = false;

        Selection = null;
    }

    public void Add(IDownloadItem downloadItem)
    {
        ArgumentNullException.ThrowIfNull(downloadItem);

        _downloadItems.Add(downloadItem);
    }

    public void Remove(IDownloadItem downloadItem)
    {
        ArgumentNullException.ThrowIfNull(downloadItem);

        _downloadItems.Remove(downloadItem);
    }

    public void Clean()
    {
        int i = 0;
        while (i < _downloadItems.Count)
        {
            var downloadItem = _downloadItems[i];
            if (downloadItem.Status.IsCompleted())
            {
                _downloadItems.RemoveAt(i);
            }
            else
            {
                i++;
            }
        }
    }

    [RelayCommand]
    public void Pause()
    {
        foreach (var download in SelectedDownloadItems)
        {
            download.Pause();
        }
    }

    [RelayCommand]
    public void Resume()
    {
        foreach (var download in SelectedDownloadItems)
        {
            download.Resume();
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        foreach (var download in SelectedDownloadItems)
        {
            download.Cancel();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        Utils.DisposeAndSetNull(ref _cleanTimer);
    }

    /// <summary>
    /// Mirrors the download item list into the view model list
    /// (replaces DynamicData's <c>Transform</c> + <c>Bind</c>).
    /// </summary>
    private void OnDownloadItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                {
                    if (e.NewItems is { } newItems && e.NewStartingIndex >= 0)
                    {
                        for (int i = 0; i < newItems.Count; i++)
                        {
                            if (newItems[i] is IDownloadItem downloadItem)
                            {
                                _downloadItemViewModels.Insert(e.NewStartingIndex + i, new DownloadItemViewModel(downloadItem));
                            }
                        }
                    }
                    break;
                }
            case NotifyCollectionChangedAction.Remove:
                {
                    if (e.OldItems is { } oldItems && e.OldStartingIndex >= 0)
                    {
                        for (int i = 0; i < oldItems.Count; i++)
                        {
                            if (e.OldStartingIndex < _downloadItemViewModels.Count)
                            {
                                // The removed item may still hold a refresh timer; release it explicitly.
                                _downloadItemViewModels[e.OldStartingIndex].Deactivate();
                                _downloadItemViewModels.RemoveAt(e.OldStartingIndex);
                            }
                        }
                    }
                    break;
                }
            case NotifyCollectionChangedAction.Replace:
                {
                    if (e.OldItems is { } oldItems && e.NewItems is { } newItems && e.OldStartingIndex >= 0)
                    {
                        for (int i = 0; i < oldItems.Count && i < newItems.Count; i++)
                        {
                            int index = e.OldStartingIndex + i;
                            if (index < _downloadItemViewModels.Count)
                            {
                                _downloadItemViewModels[index].Deactivate();
                                if (newItems[i] is IDownloadItem downloadItem)
                                {
                                    _downloadItemViewModels[index] = new DownloadItemViewModel(downloadItem);
                                }
                                else
                                {
                                    _downloadItemViewModels.RemoveAt(index);
                                }
                            }
                        }
                    }
                    break;
                }
            case NotifyCollectionChangedAction.Move:
                {
                    if (e.OldStartingIndex >= 0 && e.NewStartingIndex >= 0
                        && e.OldStartingIndex < _downloadItemViewModels.Count
                        && e.NewStartingIndex < _downloadItemViewModels.Count)
                    {
                        _downloadItemViewModels.Move(e.OldStartingIndex, e.NewStartingIndex);
                    }
                    break;
                }
            case NotifyCollectionChangedAction.Reset:
                {
                    foreach (var downloadItemViewModel in _downloadItemViewModels)
                    {
                        downloadItemViewModel.Deactivate();
                    }
                    _downloadItemViewModels.Clear();
                    break;
                }
        }
    }
}
