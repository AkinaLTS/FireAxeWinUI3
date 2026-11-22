// Ported from FireAxe_Alt\FireAxe.GUI\Views\OperationsProgressViewMessageTemplates.cs
namespace FireAxe.Views;

public static class OperationsProgressViewMessageTemplates
{
    public static OperationsProgressViewMessageTemplate Deletion { get; } = new()
    {
        Progress = Resources.Texts.OperationsProgress_Deletion_Progress,
        ProgressWithFailure = Resources.Texts.OperationsProgress_Deletion_ProgressWithFailure,
        Done = Resources.Texts.OperationsProgress_Deletion_Done,
        DoneWithFailure = Resources.Texts.OperationsProgress_Deletion_DoneWithFailure,
        OperationSucceeded = Resources.Texts.OperationsProgress_Deletion_OperationSucceeded,
        OperationFailed = Resources.Texts.OperationsProgress_Deletion_OperationFailed,
    };
}
