// Replaces the Avalonia <c>DynamicResource AddonTileSize</c> that the explorer mutated whenever
// AddonNodeExplorerViewModel.TileViewSize changed (FireAxe_Alt\FireAxe.GUI\Views\AddonNodeExplorerView.axaml.cs,
// ConnectViewModel). WinUI 3 has no DynamicResource, and a tile lives inside a DataTemplate where an
// ElementName binding back to the explorer does not resolve, so the tile edge travels through this tiny
// ambient holder instead: the explorer pushes, every live tile picks the value up.
using System;

namespace FireAxe.Views.Controls;

/// <summary>Ambient tile edge length shared by the explorer and the tiles it realizes.</summary>
public static class AddonTileMetrics
{
    public const double DefaultTileSize = 100;

    private static double s_tileSize = DefaultTileSize;

    /// <summary>Edge length of the icons inside a tile.</summary>
    public static double TileSize => s_tileSize;

    /// <summary>Raised after <see cref="TileSize"/> changed.</summary>
    public static event Action<double>? TileSizeChanged;

    public static void SetTileSize(double tileSize)
    {
        if (s_tileSize.Equals(tileSize))
        {
            return;
        }

        s_tileSize = tileSize;
        TileSizeChanged?.Invoke(tileSize);
    }
}
