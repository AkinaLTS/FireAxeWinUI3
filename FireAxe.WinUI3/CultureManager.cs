// Ported from FireAxe_Alt\FireAxe.GUI\CultureManager.cs
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;

namespace FireAxe;

/// <summary>
/// Owns the UI culture.
/// </summary>
/// <remarks>
/// The Avalonia version also called <c>SemiTheme.OverrideLocaleResources</c>; the Fluent/WinUI theme
/// has no equivalent hook, and P0 §S2 established that changing the culture does not update texts
/// already materialized in XAML (the plan accepts "restart the window", R7).
/// </remarks>
public sealed class CultureManager
{
    private static readonly FrozenSet<string> s_supportedCultureStrings = ["en", "zh-Hans"];

    private static readonly Lazy<CultureManager> s_instance = new(() => new CultureManager());

    private readonly CultureInfo _defaultCulture = CultureInfo.CurrentUICulture;

    private CultureManager()
    {
    }

    public static IReadOnlySet<string> SupportedCultureStrings => s_supportedCultureStrings;

    public static CultureManager Instance => s_instance.Value;

    public CultureInfo DefaultCulture => _defaultCulture;

    public void SetCurrentCulture(CultureInfo? culture)
    {
        culture ??= DefaultCulture;

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public void SetCurrentCulture(string? cultureStr)
    {
        SetCurrentCulture(cultureStr is null ? null : new CultureInfo(cultureStr));
    }
}
