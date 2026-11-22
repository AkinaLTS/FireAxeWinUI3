// Ported from FireAxe_Alt\FireAxe.GUI\Views\CommonMessageBoxes.cs + 56 Interaction<T,U> in ViewModels\
using System;
using System.Threading.Tasks;

namespace FireAxe.Services;

/// <summary>
/// Replaces the ~35 confirm/info/error/input <c>Interaction&lt;T,U&gt;</c> handlers of the Avalonia app.
/// Message text is composed by the ViewModel (it owns the localized strings); the service only renders it.
/// </summary>
public interface IDialogService
{
    /// <summary>Yes/No confirmation. Returns true only for "yes". Default title: <c>Texts.Confirm</c>.</summary>
    Task<bool> ConfirmAsync(string message, string? title = null);

    /// <summary>Error box with a single OK button. Default title: <c>Texts.Error</c>.</summary>
    Task ShowErrorAsync(string message, string? title = null);

    /// <summary>
    /// Error box for an exception: the exception is explained through
    /// <see cref="FireAxe.ObjectExplanationManager"/> and appended to <paramref name="message"/>.
    /// </summary>
    Task ShowExceptionAsync(Exception exception, string? message = null, string? title = null);

    /// <summary>Information box with a single OK button.</summary>
    Task ShowInfoAsync(string message, string? title = null);

    /// <summary>Text input box. Returns null when cancelled. Default title: <c>Texts.Error</c>.</summary>
    Task<string?> InputTextAsync(string message, string title, string defaultValue = "");

    /// <summary>
    /// Skip / SkipAll / Abort box used inside batch operations
    /// (was <c>Interaction&lt;(Exception, string), OperationInterruptReply&gt;</c>).
    /// </summary>
    Task<OperationInterruptReply> ShowOperationInterruptAsync(string message, string? title = null);

    /// <summary>
    /// Shows <paramref name="message"/> with an arbitrary set of buttons and returns the index of the
    /// clicked one, or -1 when the dialog was dismissed. Used by e.g. the update prompt
    /// (<c>GoToDownload</c> / <c>Ignore</c>), which has no yes/no equivalent.
    /// </summary>
    Task<int> ShowChoiceAsync(string message, string? title, params string[] buttonTexts);
}
