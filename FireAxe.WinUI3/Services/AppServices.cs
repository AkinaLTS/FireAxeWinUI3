// Ported from FireAxe_Alt\FireAxe.GUI\App.axaml.cs (DI container) — adapted for the service-locator
// access needed by the child ViewModels that are constructed with `new` inside other ViewModels.
using System;
using Microsoft.Extensions.DependencyInjection;

namespace FireAxe.Services;

/// <summary>
/// Composition-root access point for services.
/// </summary>
/// <remarks>
/// Top-level ViewModels still take their dependencies through the constructor (as in the Avalonia app).
/// This static bridge exists only for the deeply nested ViewModels that the Avalonia code created with
/// <c>new AddonNodeSimpleViewModel(addon)</c> and friends: those constructors take no service arguments,
/// and the porting rule (§6.3 "don't refactor while porting") says not to change their signatures.
/// P2 fills <see cref="Provider"/> once at startup; tests may assign their own provider.
/// </remarks>
public static class AppServices
{
    public static IServiceProvider? Provider { get; set; }

    /// <summary>Resolves a required service, throwing when the composition root is not set up.</summary>
    public static T GetRequiredService<T>() where T : notnull
    {
        return (Provider ?? throw new InvalidOperationException($"{nameof(AppServices)}.{nameof(Provider)} is not set."))
            .GetRequiredService<T>();
    }

    /// <summary>Resolves an optional service. Returns default when the provider or the registration is missing.</summary>
    public static T? TryGetService<T>() where T : class
    {
        return Provider?.GetService(typeof(T)) as T;
    }
}
