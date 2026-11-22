// Shared replacement for ReactiveUI's ViewModelActivator plumbing.
//
// Avalonia/FireAxe_Alt got this for free: every view derived from ReactiveUserControl<T>, which activated the
// ViewModel's activator when the control was attached to the visual tree and deactivated it on detach. WinUI
// has no such coupling, so every view that is handed an IViewModelLifecycle through DataContext has to do it
// itself. Views opt in by creating one of these in their constructor:
//
//     _lifecycle = new ViewModelLifecycleBinder(this);
//
// The binder activates whatever DataContext is an IViewModelLifecycle, but only while the view is loaded, and
// releases the previous one on DataContext changes. Activating is idempotent in the view models, so a view
// model that is also hosted elsewhere is not harmed; a view model shared by two live views is the one case
// this cannot model (the first detach wins) and none of the current views do that.
using System;
using FireAxe.ViewModels;
using Microsoft.UI.Xaml;

namespace FireAxe.Views.Controls;

internal sealed class ViewModelLifecycleBinder
{
    private readonly FrameworkElement _view;
    private IViewModelLifecycle? _activated = null;

    public ViewModelLifecycleBinder(FrameworkElement view)
    {
        ArgumentNullException.ThrowIfNull(view);
        _view = view;

        _view.Loaded += OnLoaded;
        _view.Unloaded += OnUnloaded;
        _view.DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Update();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Update();
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        Update();
    }

    private void Update()
    {
        // An unloaded view must release the view model: its decoded image and subscriptions are the whole
        // point of Deactivate (see AddonNodeSimpleViewModel).
        var target = _view.IsLoaded ? _view.DataContext as IViewModelLifecycle : null;
        if (ReferenceEquals(target, _activated))
        {
            return;
        }

        _activated?.Deactivate();
        _activated = target;
        _activated?.Activate();
    }
}
