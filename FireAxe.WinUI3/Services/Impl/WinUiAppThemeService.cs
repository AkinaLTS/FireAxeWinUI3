// Ported from FireAxe_Alt\FireAxe.GUI\AppSettings.cs (ColorTheme <-> Application.RequestedThemeVariant)
// Avalonia: Application.Current!.RequestedThemeVariant = ...  (app-wide)
// WinUI 3 : Application.RequestedTheme is read-only; the runtime switch is
//           FrameworkElement.RequestedTheme on each window's root content.
using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;

namespace FireAxe.Services;

/// <summary>
/// <see cref="IAppThemeService"/> over <see cref="FrameworkElement.RequestedTheme"/>.
/// </summary>
/// <remarks>
/// Every window created by <see cref="WindowService"/> registers itself here; windows created later
/// inherit the current value at registration time, so the theme survives opening new windows.
/// </remarks>
public sealed class WinUiAppThemeService : IAppThemeService
{
    private readonly List<WeakReference<Window>> _windows = [];

    private AppColorTheme _colorTheme = AppColorTheme.Default;

    public AppColorTheme ColorTheme
    {
        get => _colorTheme;
        set
        {
            if (_colorTheme == value)
            {
                return;
            }

            _colorTheme = value;
            ApplyToAllWindows();
            ColorThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? ColorThemeChanged = null;

    /// <summary>Applies the current theme to <paramref name="window"/> and keeps tracking it.</summary>
    public void RegisterWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        _windows.Add(new WeakReference<Window>(window));
        Apply(window);
    }

    /// <summary>Applies the current theme to <paramref name="window"/> again (after its content changed).</summary>
    public void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (window.Content is FrameworkElement root)
        {
            root.RequestedTheme = ToElementTheme(_colorTheme);
            window.AppWindow.TitleBar.PreferredTheme = _colorTheme switch
            {
                AppColorTheme.Light => Microsoft.UI.Windowing.TitleBarTheme.Light,
                AppColorTheme.Dark => Microsoft.UI.Windowing.TitleBarTheme.Dark,
                _ => Microsoft.UI.Windowing.TitleBarTheme.UseDefaultAppMode,
            };
        }
    }

    private void ApplyToAllWindows()
    {
        for (int i = _windows.Count - 1; i >= 0; i--)
        {
            if (_windows[i].TryGetTarget(out var window))
            {
                Apply(window);
            }
            else
            {
                _windows.RemoveAt(i);
            }
        }
    }

    private static ElementTheme ToElementTheme(AppColorTheme colorTheme)
    {
        return colorTheme switch
        {
            AppColorTheme.Light => ElementTheme.Light,
            AppColorTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }
}
