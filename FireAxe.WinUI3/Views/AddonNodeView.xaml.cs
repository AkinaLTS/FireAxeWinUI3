// Ported from FireAxe_Alt\FireAxe.GUI\Views\AddonNodeView.axaml.cs
//
// Avalonia specifics that had to change:
//   * ReactiveUserControl<T>.ViewModel / WhenAnyValue -> a plain DataContextChanged subscription. The view
//     model is whatever the explorer put into DataContext (see AddonNodeViewModel.Create).
//   * ShowDialog(GetRootWindow()) for the tag editor / customize image windows -> IWindowService. Those two
//     windows belong to P4; the service calls are placeholders that log a warning until then (P2 rule 7).
//   * The commit path of the three EditableTextBlock rows: Avalonia's binding wrote the view model property
//     and surfaced the ArgumentException through data validation. WinUI swallows exceptions thrown by a
//     binding source setter, so the control raises Submitting and the handler below performs the write in a
//     try/catch of its own, feeding the message back so the editor stays open.
using System;
using FireAxe.Services;
using FireAxe.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FireAxe.Views;

public sealed partial class AddonNodeView : UserControl
{
    private AddonNodeSimpleViewModel? _parentGroupViewModel = null;

    public AddonNodeView()
    {
        InitializeComponent();

        // The explorer hands over a freshly created view model; nothing else activates it (Avalonia did that
        // through ReactiveUserControl<T>.WhenActivated). The binder keeps itself alive through the view's
        // event handlers, so it does not need a field.
        _ = new Controls.ViewModelLifecycleBinder(this);

        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>Exposed so the explorer can start renaming with F2 (Alt did the same).</summary>
    public Controls.EditableTextBlock NameEditingControl => nameEditingControl;

    private AddonNodeViewModel? ViewModel => DataContext as AddonNodeViewModel;

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        UpdateSectionViews();
        UpdateParentGroupViewModel();
    }

    private void UpdateParentGroupViewModel()
    {
        var group = ViewModel?.Addon?.Group;

        if (ReferenceEquals(group, _parentGroupViewModel?.Addon))
        {
            return;
        }

        // The reference view activates whatever it displays (see AddonRefView), so only the lifetime of the
        // view model itself is managed here.
        _parentGroupViewModel = group is null ? null : new AddonNodeSimpleViewModel(group);
        parentGroupRefView.DataContext = _parentGroupViewModel;
    }

    private void UpdateSectionViews()
    {
        sectionViewContainerControl.Children.Clear();

        switch (ViewModel)
        {
            case AddonGroupViewModel addonGroupViewModel:
                AddSectionView(new AddonGroupSectionView(), addonGroupViewModel);
                break;

            case RefAddonNodeViewModel refAddonNodeViewModel:
                AddSectionView(new RefAddonNodeSectionView(), refAddonNodeViewModel);
                break;

            case VpkAddonViewModel vpkAddonViewModel:
                AddSectionView(new VpkAddonSectionView(), vpkAddonViewModel);

                if (vpkAddonViewModel is WorkshopVpkAddonViewModel workshopVpkAddonViewModel)
                {
                    AddSectionView(new WorkshopVpkAddonSectionView(), workshopVpkAddonViewModel);
                }

                break;
        }
    }

    private void AddSectionView(Control sectionView, AddonNodeViewModel sectionViewModel)
    {
        sectionView.DataContext = sectionViewModel;
        sectionViewContainerControl.Children.Add(new AddonNodeSectionViewDecorator(sectionView));
    }

    private void OnNameSubmitting(object? sender, Controls.EditableTextBlockSubmittingEventArgs e)
    {
        var addon = ViewModel?.Addon;
        if (addon is null)
        {
            e.Cancel = true;
            return;
        }

        try
        {
            // Renaming moves the node on disk, so failures (duplicate name, invalid characters, ...) are
            // expected here and must leave the editor open.
            addon.Name = e.NewValue;
        }
        catch (Exception ex)
        {
            e.ErrorMessage = ex.Message;
        }
    }

    private void OnPrioritySubmitting(object? sender, Controls.EditableTextBlockSubmittingEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel is null)
        {
            e.Cancel = true;
            return;
        }

        try
        {
            viewModel.Priority = e.NewValue;
        }
        catch (Exception ex)
        {
            e.ErrorMessage = ex.Message;
        }
    }

    private void OnAddonIdSubmitting(object? sender, Controls.EditableTextBlockSubmittingEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel is null)
        {
            e.Cancel = true;
            return;
        }

        try
        {
            viewModel.AddonIdString = e.NewValue;
        }
        catch (Exception ex)
        {
            e.ErrorMessage = ex.Message;
        }
    }

    private void OnEditTagButtonClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.Addon is { } addon)
        {
            AppServices.TryGetService<IWindowService>()?.OpenAddonTagEditorWindow(addon);
        }
    }

    private void OnCustomizeImageButtonClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.Addon is { } addon)
        {
            AppServices.TryGetService<IWindowService>()?.OpenAddonNodeCustomizeImageWindow(addon);
        }
    }

    private void OnCheckButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.Check();
    }

    private void OnClearCachesButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.ClearCaches();
    }

    private void OnShowInFileExplorerButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.ShowInFileExplorer();
    }
}
