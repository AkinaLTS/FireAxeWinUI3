// Ported from FireAxe_Alt\FireAxe.GUI\Views\OperationsProgressViewMessageTemplate.cs
//
// Deviation: the original used `required` members. The WinUI XAML compiler emits a parameterless activation
// path for every type reachable from markup (XamlTypeInfo.g.cs) and that fails to compile against `required`
// members (CS9035), so the properties are `init` with empty defaults instead. Callers still set all six.
namespace FireAxe.Views;

/// <summary>
/// The four progress captions and the two per-operation detail captions, so that the same
/// <see cref="OperationsProgressView"/> can be reused by several windows (P5 does that).
/// </summary>
public sealed class OperationsProgressViewMessageTemplate
{
    public static OperationsProgressViewMessageTemplate Default { get; } = new()
    {
        Progress = "Operations are currently underway... ({0} / {1})",
        ProgressWithFailure = "Operations are currently underway... ({0} / {1}, {2} failures)",
        Done = "Done. {0} operations have been completed successfully.",
        DoneWithFailure = "Done. {0} operations have been completed successfully and {1} operations have failed.",
        OperationSucceeded = "Operation Succeeded: {0}",
        OperationFailed = "Operation Failed: {0}, Failure: {1}",
    };

    public string Progress { get; init; } = string.Empty;

    public string ProgressWithFailure { get; init; } = string.Empty;

    public string Done { get; init; } = string.Empty;

    public string DoneWithFailure { get; init; } = string.Empty;

    public string OperationSucceeded { get; init; } = string.Empty;

    public string OperationFailed { get; init; } = string.Empty;
}
