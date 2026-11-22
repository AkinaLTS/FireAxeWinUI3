using System;
using FireAxe.Resources;
using FireAxe.Services;
using FireAxe.ViewModels;
using FireAxe.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FireAxe.Views;

public sealed partial class AddonNameAutoSetterWindow : HostedPage
{
    public AddonNameAutoSetterWindow(AddonNameAutoSetterViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        InitializeComponent();
        ((FrameworkElement)Content).DataContext = viewModel;
        _ = new ViewModelLifecycleBinder((FrameworkElement)Content);
        Closed += OnClosed;
    }

    public AddonNameAutoSetterViewModel ViewModel { get; }

    private async void OnAddTargetsClick(object sender, RoutedEventArgs e)
    {
        var windowService = AppServices.TryGetService<IWindowService>();
        if (windowService is null)
        {
            return;
        }

        var addons = await windowService.PickAddonNodesAsync(ViewModel.AddonRoot, allowMultiple: true, Texts.AddTargets);
        if (addons is null)
        {
            return;
        }

        foreach (var addon in addons)
        {
            ViewModel.AddTargetAddon(addon.Id);
        }
    }

    private void OnRemoveTargetClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AddonNameAutoSetterViewModel.TargetAddonViewModel target })
        {
            target.Remove();
        }
    }

    private void OnRemoveJobClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AddonNameAutoSetJobViewModel job })
        {
            ViewModel.RemoveJobViewModelCommand.Execute(job);
        }
    }

    private void OnClosed(object? sender, EventArgs args)
    {
        ViewModel.Deactivate();
        ViewModel.Dispose();
    }
}
