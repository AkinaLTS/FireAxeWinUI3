// Ported from FireAxe_Alt\FireAxe.GUI\Views\CommonMessageBoxes.cs
// MsBox.Avalonia (MessageBoxManager / MessageBoxStandardParams) -> Microsoft.UI.Xaml.Controls.ContentDialog.
//
// WinUI 3 differences (see docs/phase-notes/P2-shell.md §3):
//   * ContentDialog needs an explicit XamlRoot; the Avalonia version took an owner Window and centered itself.
//   * ContentDialog has at most three buttons (Primary / Secondary / Close), which covers every call site
//     that came out of the Avalonia app (2 and 3 button prompts).
//   * ContentDialog has no icon property; the Avalonia error box showed one. Accepted as a Fluent look
//     difference (§9 D4 / R3).
//   * "Modal over modal": ContentDialog rejects a second dialog on the same XamlRoot, so every request is
//     serialized through a semaphore — the next dialog appears after the current one is dismissed.
using System;
using System.Threading;
using System.Threading.Tasks;
using FireAxe.Resources;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace FireAxe.Services;

/// <summary>
/// <see cref="IDialogService"/> over <see cref="ContentDialog"/>.
/// </summary>
public sealed class DialogService : IDialogService
{
    private readonly IActiveWindowProvider _activeWindowProvider;

    private readonly SemaphoreSlim _gate = new(1, 1);

    public DialogService(IActiveWindowProvider activeWindowProvider)
    {
        ArgumentNullException.ThrowIfNull(activeWindowProvider);
        _activeWindowProvider = activeWindowProvider;
    }

    public Task<bool> ConfirmAsync(string message, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        return ShowAndTranslateAsync(
            message,
            title ?? Texts.Confirm,
            primaryButtonText: Texts.Yes,
            secondaryButtonText: null,
            closeButtonText: Texts.No,
            translate: result => result == ContentDialogResult.Primary);
    }

    public Task ShowErrorAsync(string message, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        return ShowOkAsync(message, title ?? Texts.Error);
    }

    public Task ShowExceptionAsync(Exception exception, string? message = null, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return ShowErrorAsync(MessageComposer.CombineMessageAndException(message, exception), title);
    }

    public Task ShowInfoAsync(string message, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        // The Avalonia version defaulted the info title to "" (i.e. no title row).
        return ShowOkAsync(message, title ?? "");
    }

    public async Task<string?> InputTextAsync(string message, string title, string defaultValue = "")
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(defaultValue);

        var textBox = new TextBox { Text = defaultValue, AcceptsReturn = false };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(textBox);

        var result = await ShowCoreAsync(
            title,
            primaryButtonText: Texts.Ok,
            secondaryButtonText: null,
            closeButtonText: Texts.Cancel,
            defaultButton: ContentDialogButton.Primary,
            content: panel);

        return result == ContentDialogResult.Primary ? textBox.Text : null;
    }

    public async Task<OperationInterruptReply> ShowOperationInterruptAsync(string message, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        var result = await ShowCoreAsync(
            title ?? Texts.Error,
            primaryButtonText: Texts.Skip,
            secondaryButtonText: Texts.SkipAll,
            closeButtonText: Texts.Abort,
            defaultButton: ContentDialogButton.Primary,
            content: message);

        return result switch
        {
            ContentDialogResult.Primary => OperationInterruptReply.Skip,
            ContentDialogResult.Secondary => OperationInterruptReply.SkipAll,
            _ => OperationInterruptReply.Abort,
        };
    }

    public async Task<int> ShowChoiceAsync(string message, string? title, params string[] buttonTexts)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(buttonTexts);
        ArgumentOutOfRangeException.ThrowIfLessThan(buttonTexts.Length, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(buttonTexts.Length, 3);

        if (TryGetXamlRoot() is null)
        {
            Log.Warning("Skipped choice ContentDialog '{Title}' because no active window XamlRoot is available.", title);
            return -1;
        }

        var result = await ShowCoreAsync(
            title ?? "",
            primaryButtonText: buttonTexts[0],
            secondaryButtonText: buttonTexts.Length >= 3 ? buttonTexts[1] : null,
            closeButtonText: buttonTexts.Length >= 2 ? buttonTexts[^1] : null,
            defaultButton: ContentDialogButton.Primary,
            content: message);

        return result switch
        {
            ContentDialogResult.Primary => 0,
            ContentDialogResult.Secondary => 1,
            _ => buttonTexts.Length == 1 ? -1 : buttonTexts.Length - 1,
        };
    }

    private Task ShowOkAsync(string message, string title)
    {
        return ShowCoreAsync(
            title,
            primaryButtonText: null,
            secondaryButtonText: null,
            closeButtonText: Texts.Ok,
            defaultButton: ContentDialogButton.Close,
            content: message);
    }

    private async Task<T> ShowAndTranslateAsync<T>(
        string message,
        string title,
        string? primaryButtonText,
        string? secondaryButtonText,
        string? closeButtonText,
        Func<ContentDialogResult, T> translate)
    {
        var result = await ShowCoreAsync(
            title,
            primaryButtonText,
            secondaryButtonText,
            closeButtonText,
            ContentDialogButton.Primary,
            message);
        return translate(result);
    }

    /// <summary>
    /// Shows one dialog and returns the button that dismissed it. Serialized: WinUI allows a single
    /// open ContentDialog per XamlRoot.
    /// </summary>
    private async Task<ContentDialogResult> ShowCoreAsync(
        string title,
        string? primaryButtonText,
        string? secondaryButtonText,
        string? closeButtonText,
        ContentDialogButton defaultButton,
        object content)
    {
        await _gate.WaitAsync();
        try
        {
            if (TryGetXamlRoot() is not { } xamlRoot)
            {
                Log.Warning("Skipped ContentDialog '{Title}' because no active window XamlRoot is available.", title);
                return ContentDialogResult.None;
            }

            var dialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = title,
                Content = content is string text ? WrapText(text) : content,
                PrimaryButtonText = primaryButtonText,
                SecondaryButtonText = secondaryButtonText,
                CloseButtonText = closeButtonText,
                DefaultButton = defaultButton,
            };

            return await dialog.ShowAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Puts message text in a bounded, scrollable box.
    /// </summary>
    /// <remarks>
    /// A bare string in <see cref="ContentDialog.Content"/> does not wrap and does not scroll: the exception
    /// dialog (message + full stack trace) grew wider than the window and was clipped. The Avalonia message box
    /// scrolled instead, so this restores that behaviour. Text is selectable, which the Avalonia box did not
    /// allow — a deliberate (documented) improvement for reading stack traces.
    /// </remarks>
    private static ScrollViewer WrapText(string text)
    {
        return new ScrollViewer
        {
            Content = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
            },
            MaxWidth = MaxMessageWidth,
            MaxHeight = MaxMessageHeight,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
    }

    private const double MaxMessageWidth = 480;

    private const double MaxMessageHeight = 320;

    private XamlRoot? TryGetXamlRoot()
    {
        return _activeWindowProvider.ActiveWindow?.Content?.XamlRoot;
    }
}
