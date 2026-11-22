// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\ViewModelBase.cs
using System;

namespace FireAxe.ViewModels;

/// <summary>
/// Base class for every ViewModel.
/// </summary>
/// <remarks>
/// The base type is written fully qualified on purpose: <c>FireAxe.Core</c> declares its own
/// <see cref="FireAxe.ObservableObject"/>, and the enclosing namespace wins over <c>using</c>
/// directives, so a bare <c>: ObservableObject</c> inside <c>FireAxe.*</c> silently binds to the
/// Core type and the MVVM source generator then emits nothing (see PORTING_PLAN §4 R11 and
/// <c>phase-notes/P0-spike.md</c> 坑 1). Never inherit <c>ObservableObject</c> directly.
/// </remarks>
public abstract partial class ViewModelBase
    : global::CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
}

/// <summary>
/// Replaces <c>IActivatableViewModel</c> / <c>WhenActivated</c>.
/// </summary>
/// <remarks>
/// The Avalonia version activated a ViewModel when its view was attached to the visual tree and
/// disposed the subscriptions when it detached (which is what lets recycled list containers release
/// images and event handlers). With ReactiveUI gone, the View drives this explicitly:
/// <list type="bullet">
/// <item>View attached to a live element → <see cref="Activate"/></item>
/// <item>View detached / container recycled → <see cref="Deactivate"/></item>
/// </list>
/// Both must be idempotent and re-entrant: Activate → Deactivate → Activate is a legal sequence
/// (list virtualization does exactly that).
/// </remarks>
public interface IViewModelLifecycle
{
    void Activate();

    void Deactivate();
}
