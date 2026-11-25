// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\AddonNameAutoSetJobViewModel.cs
// Mechanisms replaced:
//   ObservableAsPropertyHelper<bool> _isSucceeded (WhenAnyValue(IsRunning, IsFailed).Select(...).ToProperty)
//     -> computed property + [NotifyPropertyChangedFor(nameof(IsSucceeded))] on IsRunning / IsFailed
//   WhenAnyValue(x => x.TargetName).Subscribe(_ => IsApplied = false) -> partial OnTargetNameChanged
//   ReactiveCommand<Unit, bool> ApplyCommand (built from the WhenAnyValue(IsApplied, Addon, TargetName) pipeline)
//     -> hand-written RelayCommand: [RelayCommand] cannot generate a command from a non-void method (MVVMTK0007),
//        so the public `bool Apply()` method and the `ApplyCommand` member name are both kept, with an
//        explicit NotifyCanExecuteChanged() fed by partial OnIsAppliedChanged / OnTargetNameChanged and PropertyChanged(Addon)
//   OnNewAddon(AddonNode, CompositeDisposable) + DisposeWith -> OnNewAddon(AddonNode, AddonSubscriptions)
//     (the subscription bundle exposed by the ported AddonNodeSimpleViewModel)
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System;
using System.ComponentModel;
using System.Threading;

namespace FireAxe.ViewModels;

public sealed partial class AddonNameAutoSetJobViewModel : AddonNodeSimpleViewModel, IDisposable
{
    private bool _disposed = false;

    private readonly CancellationTokenSource _cts = new();

    public AddonNameAutoSetJobViewModel(AddonNode addon, object? settingArg) : base(addon)
    {
        SettingArg = settingArg;

        ApplyCommand = new RelayCommand(() => Apply(), CanApply);

        PropertyChanged += OnThisPropertyChanged;

        // Initial values (a partial property cannot carry an initializer).
        TargetName = null;
        IsRunning = false;
        IsFailed = false;
        SetNameErrorMessage = null;
        IsApplied = false;

        DoJob();
    }

    public object? SettingArg { get; }

    public IRelayCommand ApplyCommand { get; }

    [ObservableProperty]
    public partial string? TargetName { get; set; }

    partial void OnTargetNameChanged(string? value)
    {
        IsApplied = false;
        NotifyApplyCanExecuteChanged();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSucceeded))]
    public partial bool IsRunning { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSucceeded))]
    public partial bool IsFailed { get; private set; }

    public bool IsSucceeded => !IsRunning && !IsFailed;

    [ObservableProperty]
    public partial string? SetNameErrorMessage { get; private set; }

    [ObservableProperty]
    public partial bool IsApplied { get; private set; }

    partial void OnIsAppliedChanged(bool value)
    {
        NotifyApplyCanExecuteChanged();
    }

    public event Action? ApplyRequested = null;

    public bool Apply()
    {
        ThrowIfDisposed();

        var addon = Addon;
        if (addon is null)
        {
            return false;
        }

        ApplyRequested?.Invoke();

        var name = TargetName;
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        bool success = false;
        try
        {
            addon.Name = name;
            success = true;
        }
        catch (Exception ex)
        {
            SetNameErrorMessage = ObjectExplanationManager.Default.Get(ex, ExceptionExplanationScene.Input);
        }
        if (success)
        {
            IsApplied = true;
        }

        return success;
    }

    public bool Retry()
    {
        ThrowIfDisposed();

        if (IsRunning)
        {
            return false;
        }

        DoJob();
        return true;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cts.Cancel();
            _cts.Dispose();

            PropertyChanged -= OnThisPropertyChanged;

            _disposed = true;
        }
    }

    protected override void OnNewAddon(AddonNode addon, AddonSubscriptions subscriptions)
    {
        base.OnNewAddon(addon, subscriptions);

        // Was: addon.WhenAnyValue(x => x.Name).Subscribe(_ => IsApplied = false).DisposeWith(disposables);
        subscriptions.Add(addon, nameof(AddonNode.Name), OnAddonNameChanged);
    }

    private bool CanApply()
    {
        if (IsApplied)
        {
            return false;
        }
        if (Addon is null)
        {
            return false;
        }
        if (string.IsNullOrEmpty(TargetName))
        {
            return false;
        }
        return true;
    }

    private void NotifyApplyCanExecuteChanged()
    {
        // The command is created before the first notification can be raised, but a base-class property
        // change is possible during construction, hence the null check.
        ApplyCommand?.NotifyCanExecuteChanged();
    }

    private void OnThisPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Addon))
        {
            NotifyApplyCanExecuteChanged();
        }
    }

    private void OnAddonNameChanged()
    {
        IsApplied = false;
    }

    private async void DoJob()
    {
        var addon = Addon;
        if (addon is null)
        {
            return;
        }

        var cancellationToken = _cts.Token;

        IsFailed = false;
        SetNameErrorMessage = null;
        IsRunning = true;

        string? name = null;
        try
        {
            name = await addon.TryGetSuggestedNameAsync(SettingArg, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during AddonNameAutoSetJobViewModel.DoJob.");
            IsFailed = true;
            return;
        }
        finally
        {
            IsRunning = false;
        }

        if (name is null)
        {
            IsFailed = true;
            return;
        }

        TargetName = name;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }
}
