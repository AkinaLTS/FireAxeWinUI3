// Ported from FireAxe_Alt\FireAxe.GUI\Views\AddonNodeDependenciesView.axaml.cs
// AddonNodePickerWindow is a P4 window, so the picker request goes through IWindowService instead of
// instantiating the window here (P2 rule 7: every not-yet-ported window funnels through OpenNotPortedYet).
using System.Threading.Tasks;
using FireAxe.Resources;
using FireAxe.Services;
using FireAxe.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FireAxe.Views;

public sealed partial class AddonNodeDependenciesView : UserControl
{
    public AddonNodeDependenciesView()
    {
        InitializeComponent();

        // AddonNodeDependenciesViewModel builds its collection inside Activate(); the explorer does not
        // activate the view models it creates (Avalonia did that through ReactiveUserControl<T>).
        _ = new Controls.ViewModelLifecycleBinder(this);
    }

    private async void OnAddDependencyButtonClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AddonNodeDependenciesViewModel viewModel)
        {
            return;
        }

        var addon = viewModel.Addon;

        var windowService = AppServices.TryGetService<IWindowService>();
        if (windowService is null)
        {
            return;
        }

        // The Alt version created the picker with the default AllowMultiple (true), which is what the
        // allowMultiple: true below reproduces.
        var result = await windowService.PickAddonNodesAsync(addon.Root, true, Texts.AddDependencies);
        if (result is null)
        {
            return;
        }
        if (!addon.IsValid)
        {
            return;
        }
        foreach (var dependency in result)
        {
            addon.AddDependentAddon(dependency.Id);
        }
    }

    private void OnEnableAllDependenciesButtonClick(object sender, RoutedEventArgs e)
    {
        (DataContext as AddonNodeDependenciesViewModel)?.Addon.EnableAllDependencies();
    }

    private void OnRemoveDependencyButtonClick(object sender, RoutedEventArgs e)
    {
        // The button's DataContext is the item's DependencyViewModel.
        if (sender is FrameworkElement { DataContext: AddonNodeDependenciesViewModel.DependencyViewModel dependency })
        {
            dependency.Remove();
        }
    }
}
