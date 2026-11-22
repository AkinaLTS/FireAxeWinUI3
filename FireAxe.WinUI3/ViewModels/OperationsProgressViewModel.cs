// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\OperationsProgressViewModel.cs
// Mechanisms replaced:
//   ObservableAsPropertyHelper<bool> _isDone (WhenAnyValue(CompletedOperationCount, TotalOperationCount).Select(...).ToProperty)
//     -> computed property + [NotifyPropertyChangedFor(nameof(IsDone))] on both sources
//   ReactiveCommand.Create(..., this.WhenAnyValue(x => x.IsDone)) -> [RelayCommand(CanExecute = nameof(CanClose))]
//     + [NotifyCanExecuteChangedFor(nameof(CloseCommand))]
//   IActivatableViewModel + (empty) WhenActivated -> IViewModelLifecycle
//   ViewModelActivator Activator -> dropped (ReactiveUI type; same as the other ported ViewModels)
// Note: the periodic refresh mentioned in the porting task does not exist in this ViewModel in the source
// (the source's WhenActivated body is empty and refresh is driven by TaskOperationsProgressNotifier).
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;

namespace FireAxe.ViewModels;

public partial class OperationsProgressViewModel : ViewModelBase, IViewModelLifecycle, IOperationsProgressObserver
{
    public class CompletedOperationInfo
    {
        internal CompletedOperationInfo(string operation, string? failure = null)
        {
            Operation = operation;
            Failure = failure;
        }

        public string Operation { get; }

        public string? Failure { get; }
    }

    private readonly ObservableCollection<CompletedOperationInfo> _completedOperations = new();
    private readonly ReadOnlyObservableCollection<CompletedOperationInfo> _completedOperationsReadOnly;

    private bool _isActive = false;

    public OperationsProgressViewModel()
    {
        _completedOperationsReadOnly = new(_completedOperations);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDone))]
    [NotifyCanExecuteChangedFor(nameof(CloseCommand))]
    public partial int TotalOperationCount { get; private set; }

    [ObservableProperty]
    public partial bool IsCancelable { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDone))]
    [NotifyCanExecuteChangedFor(nameof(CloseCommand))]
    public partial int CompletedOperationCount { get; private set; }

    [ObservableProperty]
    public partial int SuccessfulOperationCount { get; private set; }

    [ObservableProperty]
    public partial int FailedOperationCount { get; private set; }

    public ReadOnlyObservableCollection<CompletedOperationInfo> CompletedOperations => _completedOperationsReadOnly;

    public bool IsDone => TotalOperationCount > 0 && CompletedOperationCount == TotalOperationCount;

    [ObservableProperty]
    public partial bool HasFailedOperation { get; private set; }

    public event Action<string>? OperationSucceeded = null;

    public event Action<string, string>? OperationFailed = null;

    public event Action? CloseRequested = null;

    /// <summary>
    /// Called by the View when it is attached to the visual tree
    /// (was ReactiveUI's <c>WhenActivated</c>, whose body was empty).
    /// </summary>
    public void Activate()
    {
        if (_isActive)
        {
            return;
        }
        _isActive = true;
    }

    /// <summary>
    /// Called by the View when it is detached (was the end of <c>WhenActivated</c>, which had nothing to release).
    /// </summary>
    public void Deactivate()
    {
        if (!_isActive)
        {
            return;
        }
        _isActive = false;
    }

    [RelayCommand(CanExecute = nameof(CanClose))]
    private void Close()
    {
        CloseRequested?.Invoke();
    }

    private bool CanClose()
    {
        return IsDone;
    }

    public void InitOperations(int totalOperationCount, bool isCancelable)
    {
        if (totalOperationCount <= 0)
        {
            throw new ArgumentException($"{nameof(totalOperationCount)} should be bigger than 0.");
        }
        if (TotalOperationCount != 0)
        {
            throw new InvalidOperationException("Operations had been initialized.");
        }

        TotalOperationCount = totalOperationCount;
        IsCancelable = isCancelable;
    }

    public void NotifyOperationSucceeded(string operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ThrowIfOperationsNotInited();
        ThrowIfDone();

        SuccessfulOperationCount++;
        CompletedOperationCount++;

        _completedOperations.Add(new CompletedOperationInfo(operation));

        OperationSucceeded?.Invoke(operation);
    }

    public void NotifyOperationFailed(string operation, string failure)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(failure);
        ThrowIfOperationsNotInited();
        ThrowIfDone();

        FailedOperationCount++;
        HasFailedOperation = true;
        CompletedOperationCount++;

        _completedOperations.Add(new CompletedOperationInfo(operation, failure));

        OperationFailed?.Invoke(operation, failure);
    }

    private void ThrowIfOperationsNotInited()
    {
        if (TotalOperationCount == 0)
        {
            throw new InvalidOperationException("Operations are not initialized.");
        }
    }

    private void ThrowIfDone()
    {
        if (IsDone)
        {
            throw new InvalidOperationException("Operations are done.");
        }
    }
}
