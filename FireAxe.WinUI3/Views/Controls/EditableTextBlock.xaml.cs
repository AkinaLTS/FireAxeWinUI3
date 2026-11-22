// Ported from FireAxe_Alt\FireAxe.GUI\Views\EditableTextBlock.axaml.cs
//
// Avalonia specifics that had to change:
//   * TemplatedControl + ControlTheme PART_ lookup -> a UserControl whose parts are x:Name'd in its own
//     XAML (WinUI templating would need a generic.xaml entry for no benefit here).
//   * Avalonia data validation (UpdateDataValidation / DataValidationErrors, binding writes the source
//     *synchronously* and reports the error back) has no WinUI 3 counterpart. WinUI swallows exceptions
//     thrown by a binding's source setter, so Submit() cannot learn whether `Value` was accepted.
//     Instead the control raises <see cref="Submitting"/> *before* committing anything; the host view
//     performs the write inside its own try/catch and reports failure through the event args. This keeps
//     the Alt behaviour exactly: on failure the editor stays open and the error is shown.
using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace FireAxe.Views.Controls;

/// <summary>Result of a submit attempt, used to keep the editor open when the value was rejected.</summary>
public sealed class EditableTextBlockSubmittingEventArgs : EventArgs
{
    public EditableTextBlockSubmittingEventArgs(string newValue)
    {
        NewValue = newValue;
    }

    /// <summary>The text the user typed.</summary>
    public string NewValue { get; }

    /// <summary>Set to keep the editor open without showing an error (Alt: cancel).</summary>
    public bool Cancel { get; set; }

    /// <summary>Why the value was rejected; shown next to the editor and keeps it open.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// A label that switches to a <see cref="TextBox"/> when the edit button, F2 or a double click is used.
/// Replaces Avalonia's <c>EditableTextBlock</c>; see the file header for the validation difference.
/// </summary>
public sealed partial class EditableTextBlock : UserControl
{
    /// <summary>Text to show / edit. Bound OneWay from the view model; writes go through <see cref="Submitting"/>.</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(EditableTextBlock),
        new PropertyMetadata(null, OnValueOrDisplayChanged));

    /// <summary>Overrides the displayed text (the edited text stays <see cref="Value"/>).</summary>
    public static readonly DependencyProperty DisplayProperty = DependencyProperty.Register(
        nameof(Display), typeof(string), typeof(EditableTextBlock),
        new PropertyMetadata(null, OnValueOrDisplayChanged));

    /// <summary>Placeholder shown by the editor when it is empty.</summary>
    public static readonly DependencyProperty WatermarkProperty = DependencyProperty.Register(
        nameof(Watermark), typeof(string), typeof(EditableTextBlock),
        new PropertyMetadata("", OnWatermarkChanged));

    /// <summary>
    /// Alignment applied to both the label and the editor. Avalonia inherited this from the ambient
    /// theme; WinUI has no such inheritance, so it is an explicit property.
    /// </summary>
    public static readonly DependencyProperty TextAlignmentProperty = DependencyProperty.Register(
        nameof(TextAlignment), typeof(Microsoft.UI.Xaml.TextAlignment), typeof(EditableTextBlock),
        new PropertyMetadata(Microsoft.UI.Xaml.TextAlignment.Left, OnTextAlignmentChanged));

    /// <summary>
    /// Wrapping applied to both the label and the editor. Same reasoning as <see cref="TextAlignment"/>:
    /// WinUI text controls do not inherit it from the templated parent.
    /// </summary>
    public static readonly DependencyProperty TextWrappingProperty = DependencyProperty.Register(
        nameof(TextWrapping), typeof(Microsoft.UI.Xaml.TextWrapping), typeof(EditableTextBlock),
        new PropertyMetadata(Microsoft.UI.Xaml.TextWrapping.NoWrap, OnTextWrappingChanged));

    private bool _isEditing = false;

    public EditableTextBlock()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;

        // Alt: OnKeyDown on the control, so Enter/Esc work while the editor has focus.
        KeyDown += OnKeyDown;
    }

    /// <summary>Raised by <see cref="Submit"/> before anything is written.</summary>
    public event EventHandler<EditableTextBlockSubmittingEventArgs>? Submitting;

    public string? Value
    {
        get => (string?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string? Display
    {
        get => (string?)GetValue(DisplayProperty);
        set => SetValue(DisplayProperty, value);
    }

    public string Watermark
    {
        get => (string?)GetValue(WatermarkProperty) ?? "";
        set => SetValue(WatermarkProperty, value);
    }

    public Microsoft.UI.Xaml.TextAlignment TextAlignment
    {
        get => (Microsoft.UI.Xaml.TextAlignment)GetValue(TextAlignmentProperty);
        set => SetValue(TextAlignmentProperty, value);
    }

    public bool IsEditing => _isEditing;

    public Microsoft.UI.Xaml.TextWrapping TextWrapping
    {
        get => (Microsoft.UI.Xaml.TextWrapping)GetValue(TextWrappingProperty);
        set => SetValue(TextWrappingProperty, value);
    }

    /// <summary>The editor, exposed like the Alt control did (used by tests and by the F2 flow).</summary>
    public TextBox TextBox => PART_TextBox;

    public void StartEditing()
    {
        if (_isEditing)
        {
            return;
        }

        ShowError(null);
        UpdateVisibility(true);
        _isEditing = true;

        PART_TextBox.Text = Value ?? "";
        PART_TextBox.Focus(FocusState.Programmatic);
        PART_TextBox.SelectAll();
    }

    public void EndEditing()
    {
        if (!_isEditing)
        {
            return;
        }

        UpdateVisibility(false);
        _isEditing = false;
    }

    public void Submit()
    {
        if (!_isEditing)
        {
            return;
        }

        var newValue = PART_TextBox.Text;
        var args = new EditableTextBlockSubmittingEventArgs(newValue);

        if (Submitting is { } handler)
        {
            handler(this, args);
        }
        else
        {
            // Nobody validated it: commit to the DP like the Alt control's default (unbound) case.
            Value = newValue;
        }

        if (args.ErrorMessage is { Length: > 0 } error)
        {
            ShowError(error);
            return;
        }
        if (args.Cancel)
        {
            return;
        }

        ShowError(null);
        EndEditing();
    }

    private static void OnValueOrDisplayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (EditableTextBlock)d;
        control.UpdateDisplayText();
    }

    private static void OnWatermarkChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((EditableTextBlock)d).PART_TextBox.PlaceholderText = (string?)e.NewValue ?? "";
    }

    private static void OnTextAlignmentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (EditableTextBlock)d;
        var alignment = (Microsoft.UI.Xaml.TextAlignment)e.NewValue;
        control.PART_TextBlock.TextAlignment = alignment;
        control.PART_TextBox.TextAlignment = alignment;
    }

    private static void OnTextWrappingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (EditableTextBlock)d;
        var wrapping = (Microsoft.UI.Xaml.TextWrapping)e.NewValue;
        control.PART_TextBlock.TextWrapping = wrapping;
        control.PART_TextBox.TextWrapping = wrapping;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Alt's theme inherited the ambient text settings; WinUI's TextBox does not, so copy them across.
        PART_TextBlock.FontSize = FontSize;
        PART_TextBlock.FontWeight = FontWeight;
        PART_TextBlock.TextAlignment = TextAlignment;
        PART_TextBlock.TextWrapping = TextWrapping;
        PART_TextBox.FontSize = FontSize;
        PART_TextBox.FontWeight = FontWeight;
        PART_TextBox.TextAlignment = TextAlignment;
        PART_TextBox.TextWrapping = TextWrapping;
        PART_TextBox.PlaceholderText = Watermark ?? "";

        UpdateDisplayText();
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        // Alt: WhenAnyValue(DataContext) -> EndEditing. The recycled container must not keep editing
        // an addon it no longer shows.
        EndEditing();
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        PART_EditButton.Opacity = 1.0;
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        PART_EditButton.Opacity = 0.45;
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_isEditing)
        {
            return;
        }

        if (e.Key == VirtualKey.Enter && !IsShiftDown())
        {
            Submit();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            EndEditing();
            e.Handled = true;
        }
    }

    private void OnEditButtonClick(object sender, RoutedEventArgs e)
    {
        StartEditing();
    }

    private void OnSubmitButtonClick(object sender, RoutedEventArgs e)
    {
        Submit();
    }

    private void OnCancelButtonClick(object sender, RoutedEventArgs e)
    {
        EndEditing();
    }

    private void UpdateDisplayText()
    {
        PART_TextBlock.Text = Display ?? Value ?? "";
    }

    private void UpdateVisibility(bool editing)
    {
        PART_DisplayView.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
        PART_EditView.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;

        if (!editing)
        {
            PART_TextBox.Text = "";
            ShowError(null);
        }
    }

    private void ShowError(string? message)
    {
        if (message is null)
        {
            ToolTipService.SetToolTip(PART_TextBox, null);
            PART_TextBox.ClearValue(Control.BorderBrushProperty);
            PART_TextBox.BorderThickness = new Thickness(1);
            return;
        }

        ToolTipService.SetToolTip(PART_TextBox, message);
        PART_TextBox.BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["FireAxeWarningBrush"];
        PART_TextBox.BorderThickness = new Thickness(2);
    }

    private static bool IsShiftDown()
    {
        var state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
        return state.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
    }
}
