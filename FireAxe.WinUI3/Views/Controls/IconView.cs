// WinUI 3 replacement for Avalonia's `<PathIcon Data="{StaticResource ...}" />` usage of the shared
// StreamGeometry resources in Assets/Icons.axaml.
//
// A Geometry materialized by XamlReader cannot be shared: the XAML parser attaches the parsed geometry
// to the element that owns it, and assigning it to a second PathIcon/Path throws
//     System.ArgumentException: Value does not fall within the expected range.
// (verified by probe, see docs/phase-notes/P3-*.md). So this control keeps only the path *string* and
// parses it into its own private element once per instance; the element is never handed to anyone else.
//
// Usage:
//     <controls:IconView Data="{x:Bind local:Icons.Home, Mode=OneTime}" Width="25" Height="25" />
// The fill follows Foreground (like PathIcon); Stroke/StrokeThickness are optional and mirror the
// outline icons of the Alt build (the small warning triangles and the enable button).
using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using ShapePath = Microsoft.UI.Xaml.Shapes.Path;
using Shape = Microsoft.UI.Xaml.Shapes.Shape;

namespace FireAxe.Views.Controls;

/// <summary>
/// Displays an icon from abbreviated path markup. Each instance parses the markup itself because WinUI
/// cannot share a parsed <see cref="Geometry"/> between elements.
/// </summary>
public sealed class IconView : ContentControl
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data),
        typeof(string),
        typeof(IconView),
        new PropertyMetadata(null, OnDataChanged));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke),
        typeof(Brush),
        typeof(IconView),
        new PropertyMetadata(null, OnStrokeChanged));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness),
        typeof(double),
        typeof(IconView),
        new PropertyMetadata(0d, OnStrokeThicknessChanged));

    public IconView()
    {
        IsTabStop = false;
        Padding = new Thickness(0);
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        RegisterPropertyChangedCallback(ForegroundProperty, (_, _) => ApplyFill());
        SizeChanged += (_, _) => ApplySize();
    }

    /// <summary>Icon geometry as abbreviated path markup (see <see cref="Icons"/>).</summary>
    public string? Data
    {
        get => (string?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <summary>Optional outline brush (the fill always follows <see cref="Control.Foreground"/>).</summary>
    public Brush? Stroke
    {
        get => (Brush?)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((IconView)d).UpdateContent();
    }

    private static void OnStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((IconView)d).ApplyStroke();
    }

    private static void OnStrokeThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((IconView)d).ApplyStroke();
    }

    private void UpdateContent()
    {
        string? data = Data;
        Content = string.IsNullOrEmpty(data) ? null : CreatePath(data);
        ApplyStroke();
        ApplyFill();
        ApplySize();
    }

    private static ShapePath CreatePath(string data)
    {
        // Only the XAML parser understands abbreviated path markup; the element it produces is used
        // as-is (never copied), which is what makes this legal.
        string markup =
            "<Path xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" Data=\""
            + data
            + "\" Stretch=\"Uniform\" />";
        return (ShapePath)XamlReader.Load(markup);
    }

    /// <summary>
    /// A <see cref="Shape"/> with <see cref="Shape.Stretch"/> = Uniform reports its own geometry bounds
    /// as the desired size, so the size of this control has to be pushed down explicitly to make the
    /// icon scale the way <see cref="PathIcon"/> does.
    /// </summary>
    private void ApplySize()
    {
        if (Content is ShapePath path && ActualWidth > 0 && ActualHeight > 0)
        {
            path.Width = ActualWidth;
            path.Height = ActualHeight;
        }
    }

    private void ApplyStroke()
    {
        if (Content is ShapePath path)
        {
            path.Stroke = Stroke;
            path.StrokeThickness = StrokeThickness;
        }
    }

    private void ApplyFill()
    {
        if (Content is ShapePath path)
        {
            path.Fill = Foreground;
        }
    }
}
