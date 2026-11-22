// Minimal replacement for the two DynamicData operators the Avalonia port used in
// AddonNodeExplorerViewModel.RefreshCurrentNodes():
//
//     rawNodes.ToObservableChangeSet()
//             .Sort(new AddonNodeComparer(...))
//             .Bind(out ReadOnlyObservableCollection<AddonNode> nodes)
//
// The important property of that pipeline is that it stays *live*: the search runs
// asynchronously and streams matches into the source collection after Bind() returned,
// so a snapshot copy would silently drop every result. This class keeps the sorted view
// in sync by subscribing to the source's CollectionChanged and inserting each new item
// in comparer order.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace FireAxe.Collections;

/// <summary>
/// An <see cref="ObservableCollection{T}"/> that mirrors a source collection in the order
/// given by an <see cref="IComparer{T}"/>, including items added to the source later.
/// </summary>
public sealed class SortedObservableCollection<T> : ObservableCollection<T>, IDisposable
{
    private readonly IEnumerable<T>? _source;
    private readonly IComparer<T> _comparer;
    private bool _disposed;

    public SortedObservableCollection(IEnumerable<T> source, IComparer<T> comparer)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(comparer);

        _source = source;
        _comparer = comparer;

        foreach (var item in source)
        {
            Items.Insert(FindInsertionIndex(item), item);
        }

        if (source is INotifyCollectionChanged observable)
        {
            observable.CollectionChanged += OnSourceCollectionChanged;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        if (_source is INotifyCollectionChanged observable)
        {
            observable.CollectionChanged -= OnSourceCollectionChanged;
        }
    }

    private int FindInsertionIndex(T item)
    {
        int low = 0;
        int high = Count;
        while (low < high)
        {
            int mid = low + ((high - low) / 2);
            if (_comparer.Compare(Items[mid], item) <= 0)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }
        return low;
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems is { } newItems:
                foreach (T item in newItems)
                {
                    if (item is null)
                    {
                        continue;
                    }
                    Insert(FindInsertionIndex(item), item);
                }
                break;

            case NotifyCollectionChangedAction.Remove when e.OldItems is { } oldItems:
                foreach (T item in oldItems)
                {
                    Remove(item);
                }
                break;

            // Reset / Move / Replace all change the relative order of many items at once;
            // rebuilding from the source is cheaper to reason about than patching in place.
            default:
                Rebuild();
                break;
        }
    }

    private void Rebuild()
    {
        Items.Clear();
        if (_source is not null)
        {
            foreach (var item in _source)
            {
                Items.Insert(FindInsertionIndex(item), item);
            }
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
