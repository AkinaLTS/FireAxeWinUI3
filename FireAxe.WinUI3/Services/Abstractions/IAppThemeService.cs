// Ported from FireAxe_Alt\FireAxe.GUI\AppSettings.cs (ColorTheme <-> Application.RequestedThemeVariant)
namespace FireAxe.Services;

/// <summary>
/// Abstraction over the application colour theme. Lets <see cref="AppSettings"/> persist and
/// report the theme without referencing <c>Microsoft.UI.Xaml.Application</c>.
/// </summary>
public interface IAppThemeService
{
    /// <summary>The effective theme requested by the app.</summary>
    AppColorTheme ColorTheme { get; set; }

    /// <summary>Raised after <see cref="ColorTheme"/> changed.</summary>
    event EventHandler? ColorThemeChanged;
}
