// Ported from FireAxe_Alt\FireAxe.GUI\Views\AddonNodeTileView.axaml.cs
// The Avalonia base class became the shared binder (see AddonNodeListItemViewModelBinder.cs); the tile edge
// came from a DynamicResource (AddonTileSize) and is now pushed through AddonTileMetrics, with the
// ImageSize dependency property kept as an explicit override for callers that need a fixed edge.
using FireAxe.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FireAxe.Views.Controls;

/// <summary>One addon drawn as a square tile (the explorer's tile view).</summary>
public sealed partial class AddonNodeTileView : UserControl
{
    public static readonly DependencyProperty ImageSizeProperty = DependencyProperty.Register(
        nameof(ImageSize), typeof(double), typeof(AddonNodeTileView),
        new PropertyMetadata(AddonTileMetrics.DefaultTileSize, OnImageSizeChanged));

    private readonly AddonNodeListItemViewModelBinder _binder;

    public AddonNodeTileView()
    {
        InitializeComponent();
        _binder = new AddonNodeListItemViewModelBinder(this, rootGrid, addon => new AddonNodeListItemViewModel(addon));
        ImageSize = AddonTileMetrics.TileSize;
        ApplyImageSize();

        // Tiles are recycled by the ListView, so follow the ambient edge only while loaded.
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>Edge length of the icon inside the tile (the Alt <c>AddonTileSize</c> resource).</summary>
    public double ImageSize
    {
        get => (double)GetValue(ImageSizeProperty);
        set => SetValue(ImageSizeProperty, value);
    }

    /// <summary>The item view model this tile is currently showing.</summary>
    public AddonNodeListItemViewModel? ViewModel => _binder.ViewModel;

    private static void OnImageSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((AddonNodeTileView)d).ApplyImageSize();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AddonTileMetrics.TileSizeChanged += OnTileSizeChanged;
        ImageSize = AddonTileMetrics.TileSize;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        AddonTileMetrics.TileSizeChanged -= OnTileSizeChanged;
    }

    private void OnTileSizeChanged(double tileSize)
    {
        ImageSize = tileSize;
    }

    private void ApplyImageSize()
    {
        double size = ImageSize;
        iconView.ImageSize = size;
        // Alt: SpecialIconSize="{Binding $self.ImageSize}" -- the folder/question icons track the tile edge.
        iconView.SpecialIconSize = size;
    }
}
