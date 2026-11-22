// Ported from FireAxe_Alt\FireAxe.GUI\Views\OperationsProgressView.axaml.cs
//   WhenAnyValue(CompletedOperationCount, TotalOperationCount, FailedOperationCount, IsDone)
//     .CombineLatest(MessageTemplate).Select(...).BindTo(Message)   -> ComputeMessage() on PropertyChanged
//   CompletedOperations.ToObservableChangeSet().Transform(...)     -> Subscribe to the ViewModel's
//                                                                     OperationSucceeded / OperationFailed events
//   StyledProperty/DirectProperty MessageTemplate/Message/DetailedMessages -> plain CLR properties + x:Bind
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using FireAxe.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FireAxe.Views;

public sealed partial class OperationsProgressView : UserControl, INotifyPropertyChanged
{
    private OperationsProgressViewModel? _viewModel = null;
    private OperationsProgressViewMessageTemplate _messageTemplate = OperationsProgressViewMessageTemplate.Default;
    private string _message = "";

    public OperationsProgressView()
    {
        InitializeComponent();

        Loaded += (sender, args) => _viewModel?.Activate();
        Unloaded += (sender, args) => _viewModel?.Deactivate();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// The observed ViewModel. Set from code (the Avalonia version got it from the DataContext).
    /// </summary>
    public OperationsProgressViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            if (ReferenceEquals(_viewModel, value))
            {
                return;
            }

            DetachViewModel(_viewModel);
            _viewModel = value;
            AttachViewModel(value);
            OnTrackedStateChanged();
        }
    }

    public OperationsProgressViewMessageTemplate MessageTemplate
    {
        get => _messageTemplate;
        set
        {
            if (ReferenceEquals(_messageTemplate, value) || value is null)
            {
                return;
            }
            _messageTemplate = value;
            OnTrackedStateChanged();
        }
    }

    public string Message
    {
        get => _message;
        private set
        {
            if (_message == value)
            {
                return;
            }
            _message = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Message)));
        }
    }

    public bool HasFailedOperation => _viewModel?.HasFailedOperation ?? false;

    /// <summary>The per-operation detail lines, already formatted with <see cref="MessageTemplate"/>.</summary>
    public ObservableCollection<string> DetailedMessages { get; } = [];

    private void AttachViewModel(OperationsProgressViewModel? viewModel)
    {
        if (viewModel is null)
        {
            return;
        }

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.OperationSucceeded += OnOperationSucceeded;
        viewModel.OperationFailed += OnOperationFailed;

        // Replay what already happened (a window may attach after the first operations finished).
        foreach (var operation in viewModel.CompletedOperations)
        {
            DetailedMessages.Add(FormatCompletedOperation(operation));
        }
    }

    private void DetachViewModel(OperationsProgressViewModel? viewModel)
    {
        if (viewModel is null)
        {
            return;
        }

        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        viewModel.OperationSucceeded -= OnOperationSucceeded;
        viewModel.OperationFailed -= OnOperationFailed;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnTrackedStateChanged();
    }

    private void OnOperationSucceeded(string operation)
    {
        DetailedMessages.Add(_messageTemplate.OperationSucceeded.FormatNoThrow(operation));
    }

    private void OnOperationFailed(string operation, string failure)
    {
        DetailedMessages.Add(_messageTemplate.OperationFailed.FormatNoThrow(operation, failure));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasFailedOperation)));
    }

    private string FormatCompletedOperation(OperationsProgressViewModel.CompletedOperationInfo operation)
    {
        return operation.Failure is { } failure
            ? _messageTemplate.OperationFailed.FormatNoThrow(operation.Operation, failure)
            : _messageTemplate.OperationSucceeded.FormatNoThrow(operation.Operation);
    }

    private void OnTrackedStateChanged()
    {
        Message = ComputeMessage();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasFailedOperation)));
    }

    private string ComputeMessage()
    {
        if (_viewModel is not { } viewModel)
        {
            return "";
        }

        if (viewModel.IsDone)
        {
            return viewModel.HasFailedOperation
                ? _messageTemplate.DoneWithFailure.FormatNoThrow(viewModel.SuccessfulOperationCount, viewModel.FailedOperationCount)
                : _messageTemplate.Done.FormatNoThrow(viewModel.TotalOperationCount);
        }

        return viewModel.HasFailedOperation
            ? _messageTemplate.ProgressWithFailure.FormatNoThrow(
                viewModel.CompletedOperationCount, viewModel.TotalOperationCount, viewModel.FailedOperationCount)
            : _messageTemplate.Progress.FormatNoThrow(viewModel.CompletedOperationCount, viewModel.TotalOperationCount);
    }
}
