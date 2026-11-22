using System;
using System.Threading.Tasks;
using FireAxe.Resources;
using FireAxe.Services;
using FireAxe.ViewModels;
using FireAxe.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FireAxe.Views;

public sealed partial class AddonProblemListWindow : HostedPage
{
    public AddonProblemListWindow(AddonProblemListViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        InitializeComponent();
        ((FrameworkElement)Content).DataContext = viewModel;
        _ = new ViewModelLifecycleBinder((FrameworkElement)Content);
        Closed += OnClosed;
        problemTabs.TabItems.Add(new TabViewItem { Header = Texts.Problems });
        problemTabs.TabItems.Add(new TabViewItem { Header = Texts.ProblematicAddons });
        problemTabs.SelectedIndex = 0;
        problemTabs.SelectionChanged += OnTabSelectionChanged;
    }

    public AddonProblemListViewModel ViewModel { get; }

    public int ProblemCount => ViewModel.AddonRoot.ProblemCount;

    public Task? CheckTask => ViewModel.AddonRoot.CheckTask;

    private void OnCheckClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.AddonRoot.Check();
        }
        catch (Exception exception)
        {
            _ = AppServices.GetRequiredService<IDialogService>().ShowErrorAsync(exception.ToString());
        }
    }

    private void OnTabSelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs args)
    {
        bool isProblems = problemTabs.SelectedIndex == 0;
        problemsPanel.Visibility = isProblems ? Visibility.Visible : Visibility.Collapsed;
        addonsPanel.Visibility = isProblems ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnClosed(object? sender, EventArgs args)
    {
        problemTabs.SelectionChanged -= OnTabSelectionChanged;
        ViewModel.Deactivate();
    }
}
