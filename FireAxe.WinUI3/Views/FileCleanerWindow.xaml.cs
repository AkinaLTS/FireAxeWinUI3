// Ported from FireAxe_Alt\FireAxe.GUI\Views\FileCleanerWindow.axaml.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FireAxe.Resources;
using FireAxe.Services;
using FireAxe.ViewModels;
using FireAxe.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FireAxe.Views;

public sealed partial class FileCleanerWindow : HostedPage
{
    public FileCleanerWindow(FileCleanerViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        InitializeComponent();
        ((FrameworkElement)Content).DataContext = viewModel;
        _ = new ViewModelLifecycleBinder((FrameworkElement)Content);
        Closed += OnClosed;
    }

    public FileCleanerViewModel ViewModel { get; }

    private void OnShowFileLabelLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Button button) button.Content = Texts.ShowInFileExplorer;
    }

    private void OnRetainLabelLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Button button) button.Content = Texts.Retain;
    }

    private void OnDeleteLabelLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Button button) button.Content = Texts.Delete;
    }

    private async void OnAddSourceClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsValid)
        {
            return;
        }

        var addons = await AppServices.GetRequiredService<IWindowService>()
            .PickAddonNodesAsync(ViewModel.AddonRoot, allowMultiple: true, Texts.AddSearchSource);
        if (addons is null || !ViewModel.IsValid)
        {
            return;
        }

        foreach (var addon in addons)
        {
            ViewModel.AddSourceAddon(addon.Id);
        }
    }

    private void OnClosed(object? sender, EventArgs args)
    {
        ViewModel.Deactivate();
    }
}
