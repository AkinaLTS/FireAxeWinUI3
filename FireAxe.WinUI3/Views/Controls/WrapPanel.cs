// Replacement for Avalonia's WrapPanel, which WinUI 3 does not ship in a usable form here:
// the only built-in wrapping panels (ItemsWrapGrid / ItemsStackPanel) are virtualizing panels that
// belong to ListViewBase and cannot be placed in a plain ItemsControl or a StackPanel. The three places
// that need wrapping (tag lists in the grid row, in AddonNodeView, and the "moving" strip) are small and
// non-virtualizing, so a straightforward 60-line panel is both simpler and closer to the Avalonia
// behaviour (children keep their desired size and flow onto the next line).
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace FireAxe.Views.Controls;

/// <summary>
/// Lays children out left to right, wrapping to a new line when the available width is used up
/// (Avalonia <c>WrapPanel</c> semantics: no uniform item size, no virtualization).
/// </summary>
public sealed partial class WrapPanel : Panel
{
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(Orientation), typeof(WrapPanel),
        new PropertyMetadata(Orientation.Horizontal, OnLayoutPropertyChanged));

    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
        nameof(ItemWidth), typeof(double), typeof(WrapPanel),
        new PropertyMetadata(double.NaN, OnLayoutPropertyChanged));

    public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(
        nameof(ItemHeight), typeof(double), typeof(WrapPanel),
        new PropertyMetadata(double.NaN, OnLayoutPropertyChanged));

    /// <summary>Flow direction. Only <see cref="Orientation.Horizontal"/> is actually used by the port.</summary>
    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>Uniform child width; <see cref="double.NaN"/> (the default) keeps each child's desired width.</summary>
    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    /// <summary>Uniform child height; <see cref="double.NaN"/> (the default) keeps each child's desired height.</summary>
    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((WrapPanel)d).InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var lineSize = new Size();
        var panelSize = new Size();
        bool isHorizontal = Orientation == Orientation.Horizontal;
        double mainLimit = isHorizontal ? availableSize.Width : availableSize.Height;

        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            var childSize = new Size(
                double.IsNaN(ItemWidth) ? child.DesiredSize.Width : ItemWidth,
                double.IsNaN(ItemHeight) ? child.DesiredSize.Height : ItemHeight);

            double mainSize = isHorizontal ? childSize.Width : childSize.Height;
            double crossSize = isHorizontal ? childSize.Height : childSize.Width;

            // Wrap when the next child would overflow the current line (an empty line never wraps, so a
            // single oversize child still gets laid out instead of looping forever).
            double usedMain = isHorizontal ? lineSize.Width : lineSize.Height;
            if (usedMain > 0 && usedMain + mainSize > mainLimit)
            {
                AddLine(ref panelSize, lineSize, isHorizontal);
                lineSize = new Size();
            }

            if (isHorizontal)
            {
                lineSize.Width += childSize.Width;
                lineSize.Height = Math.Max(lineSize.Height, crossSize);
            }
            else
            {
                lineSize.Height += childSize.Height;
                lineSize.Width = Math.Max(lineSize.Width, crossSize);
            }
        }

        AddLine(ref panelSize, lineSize, isHorizontal);

        return new Size(
            double.IsInfinity(availableSize.Width) ? panelSize.Width : Math.Min(panelSize.Width, availableSize.Width),
            double.IsInfinity(availableSize.Height) ? panelSize.Height : Math.Min(panelSize.Height, availableSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        bool isHorizontal = Orientation == Orientation.Horizontal;
        double mainOffset = 0;
        double crossOffset = 0;
        double lineCross = 0;

        foreach (var child in Children)
        {
            var childSize = new Size(
                double.IsNaN(ItemWidth) ? child.DesiredSize.Width : ItemWidth,
                double.IsNaN(ItemHeight) ? child.DesiredSize.Height : ItemHeight);

            double mainSize = isHorizontal ? childSize.Width : childSize.Height;
            double crossSize = isHorizontal ? childSize.Height : childSize.Width;
            double mainLimit = isHorizontal ? finalSize.Width : finalSize.Height;

            if (mainOffset > 0 && mainOffset + mainSize > mainLimit)
            {
                mainOffset = 0;
                crossOffset += lineCross;
                lineCross = 0;
            }

            var bounds = isHorizontal
                ? new Rect(mainOffset, crossOffset, childSize.Width, childSize.Height)
                : new Rect(crossOffset, mainOffset, childSize.Width, childSize.Height);

            child.Arrange(bounds);

            mainOffset += mainSize;
            lineCross = Math.Max(lineCross, crossSize);
        }

        return finalSize;
    }

    private static void AddLine(ref Size panelSize, Size lineSize, bool isHorizontal)
    {
        if (isHorizontal)
        {
            panelSize.Width = Math.Max(panelSize.Width, lineSize.Width);
            panelSize.Height += lineSize.Height;
        }
        else
        {
            panelSize.Height = Math.Max(panelSize.Height, lineSize.Height);
            panelSize.Width += lineSize.Width;
        }
    }
}
