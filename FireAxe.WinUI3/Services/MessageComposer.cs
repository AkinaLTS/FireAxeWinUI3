// Ported from FireAxe_Alt\FireAxe.GUI\Views\CommonMessageBoxes.cs (CombineMessageAndException)
using System;

namespace FireAxe.Services;

/// <summary>
/// Composes the "message + explained exception" bodies that <c>CommonMessageBoxes.ShowException</c>
/// and <c>GetOperationInterruptReply</c> used to build in the View layer.
/// </summary>
public static class MessageComposer
{
    public static string CombineMessageAndException(string? message, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        string exceptionMessage = ObjectExplanationManager.Default.Get(exception);
        return string.IsNullOrEmpty(message) ? exceptionMessage : message + '\n' + exceptionMessage;
    }
}
