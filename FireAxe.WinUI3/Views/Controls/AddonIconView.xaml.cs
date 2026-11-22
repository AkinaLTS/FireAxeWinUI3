// Ported from FireAxe_Alt\FireAxe.GUI\Views\AddonIconView.axaml.cs
//   * Avalonia WhenAnyValue(...).BindTo() -> dependency property changed callbacks writing the parts.
//   * The default sizes (50/50) and the "SpecialIconSize = ImageSize" idiom used by the tile view are
//     preserved so the very same XAML values still work.
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FireAxe.Views.Controls;

/// <summary>Shows the addon preview image, or the folder / question icon, at a given size.</summary>
public sealed partial class AddonIconView : UserControl
{
    public static readonly DependencyProperty ImageSizeProperty = DependencyProperty.Register(
        nameof(ImageSize), typeof(double), typeof(AddonIconView),
        new PropertyMetadata(50.0, OnImageSizeChanged));

    public static readonly DependencyProperty SpecialIconSizeProperty = DependencyProperty.Register(
        nameof(SpecialIconSize), typeof(double), typeof(AddonIconView),
        new PropertyMetadata(50.0, OnSpecialIconSizeChanged));

    public AddonIconView()
    {
        InitializeComponent();
    }

    /// <summary>Edge length of the preview image.</summary>
    public double ImageSize
    {
        get => (double)GetValue(ImageSizeProperty);
        set => SetValue(ImageSizeProperty, value);
    }

    /// <summary>Edge length of the folder / question icons (the Alt views pass <c>ImageSize</c> here).</summary>
    public double SpecialIconSize
    {
        get => (double)GetValue(SpecialIconSizeProperty);
        set => SetValue(SpecialIconSizeProperty, value);
    }

    private static void OnImageSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AddonIconView)d;
        var size = (double)e.NewValue;
        control.image.Width = size;
        control.image.Height = size;
    }

    private static void OnSpecialIconSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AddonIconView)d;
        var size = (double)e.NewValue;
        control.unknownIcon.Width = size;
        control.unknownIcon.Height = size;
        control.folderIcon.Width = size;
        control.folderIcon.Height = size;
    }
}
