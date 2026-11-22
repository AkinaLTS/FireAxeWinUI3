// Replaces FireAxe_Alt\FireAxe.GUI\MarkupExtensions\EnumValues.cs (Avalonia markup extension
// {me:EnumValues {x:Type ...}}). WinUI 3 has no markup-extension equivalent, so the value lists are
// exposed as static arrays that {x:Bind} function/property bindings can reach.
//
// Note: Enum.GetValues() also returns the aliases (TileBegin/TileEnd of AddonNodeListItemViewKind),
// which is why the original code called Distinct() — the arrays below are written out explicitly.
using System;
using FireAxe.ViewModels;

namespace FireAxe.Views;

/// <summary>Item sources for the enum <c>ComboBox</c>es of the explorer.</summary>
public static class EnumSources
{
    public static AddonNodeSortMethod[] AddonNodeSortMethods { get; } = BuildSortMethods();

    public static AddonNodeListItemViewKind[] AddonNodeListItemViewKinds { get; } =
    [
        AddonNodeListItemViewKind.Grid,
        AddonNodeListItemViewKind.MediumTile,
        AddonNodeListItemViewKind.SmallTile,
        AddonNodeListItemViewKind.LargeTile,
    ];

    public static AddonTagFilterMode[] AddonTagFilterModes { get; } =
    [
        AddonTagFilterMode.Or,
        AddonTagFilterMode.And,
        AddonTagFilterMode.Not,
    ];

    public static AddonGroupEnableStrategy[] AddonGroupEnableStrategies { get; } =
    [
        AddonGroupEnableStrategy.None,
        AddonGroupEnableStrategy.Single,
        AddonGroupEnableStrategy.SingleRandom,
        AddonGroupEnableStrategy.All,
    ];

    /// <summary>Enum.GetValues order, aliases removed (the Avalonia <c>EnumValues</c> did the same).</summary>
    private static AddonNodeSortMethod[] BuildSortMethods()
    {
        var values = Enum.GetValues<AddonNodeSortMethod>();
        var result = new System.Collections.Generic.List<AddonNodeSortMethod>(values.Length);
        foreach (var value in values)
        {
            if (!result.Contains(value))
            {
                result.Add(value);
            }
        }
        return [.. result];
    }
}
