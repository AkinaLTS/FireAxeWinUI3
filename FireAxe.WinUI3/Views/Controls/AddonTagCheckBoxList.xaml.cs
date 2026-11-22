// Ported from FireAxe_Alt\FireAxe.GUI\Views\AddonTagCheckBoxList.axaml.cs
//   * GetLogicalDescendants() -> a tag -> CheckBox dictionary filled by the items' Loaded/Unloaded events
//     (WinUI has no logical tree; ItemsControl is not virtualizing, so every tag stays realized).
//   * All / None set every box and then push SelectedTags once (instead of once per box).
//   * One intentional improvement over the Alt view: a freshly created box starts checked when the tag is
//     already in SelectedTags, so the list stays truthful if it is ever rebuilt.
using System;
using System.Collections.Generic;
using FireAxe.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FireAxe.Views.Controls;

/// <summary>The "All / None + one check box per existing tag" list used by the search options panel.</summary>
public sealed partial class AddonTagCheckBoxList : UserControl
{
    private readonly Dictionary<string, CheckBox> _checkBoxes = new(StringComparer.Ordinal);

    /// <summary>Set while the code changes IsChecked itself, to avoid one SelectedTags push per box.</summary>
    private bool _suppressToggleHandling;

    public AddonTagCheckBoxList()
    {
        InitializeComponent();
    }

    private AddonNodeExplorerViewModel? ViewModel => DataContext as AddonNodeExplorerViewModel;

    private void OnCheckBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox || checkBox.DataContext is not string tag)
        {
            return;
        }

        _checkBoxes[tag] = checkBox;

        if (ViewModel is { } viewModel)
        {
            bool isSelected = new HashSet<string>(viewModel.SelectedTags, StringComparer.Ordinal).Contains(tag);
            if (checkBox.IsChecked != isSelected)
            {
                _suppressToggleHandling = true;
                try
                {
                    checkBox.IsChecked = isSelected;
                }
                finally
                {
                    _suppressToggleHandling = false;
                }
            }
        }
    }

    private void OnCheckBoxUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.DataContext is string tag)
        {
            if (_checkBoxes.TryGetValue(tag, out var registered) && ReferenceEquals(registered, checkBox))
            {
                _checkBoxes.Remove(tag);
            }
        }
    }

    private void OnCheckBoxToggled(object sender, RoutedEventArgs e)
    {
        if (!_suppressToggleHandling)
        {
            PushSelectedTags();
        }
    }

    private void OnAllButtonClick(object sender, RoutedEventArgs e)
    {
        SetAll(true);
    }

    private void OnNoneButtonClick(object sender, RoutedEventArgs e)
    {
        SetAll(false);
    }

    private void SetAll(bool isChecked)
    {
        _suppressToggleHandling = true;
        try
        {
            foreach (var checkBox in _checkBoxes.Values)
            {
                checkBox.IsChecked = isChecked;
            }
        }
        finally
        {
            _suppressToggleHandling = false;
        }

        PushSelectedTags();
    }

    private void PushSelectedTags()
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var selectedTags = new List<string>();
        foreach (var (tag, checkBox) in _checkBoxes)
        {
            if (checkBox.IsChecked == true)
            {
                selectedTags.Add(tag);
            }
        }

        viewModel.SelectedTags = selectedTags;
    }
}
