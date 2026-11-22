// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\DownloadItemViewModel.cs
// Mechanisms replaced:
//   ReactiveUI IActivatableViewModel/WhenActivated + Avalonia DispatcherTimer.Run -> IViewModelLifecycle + IUiDispatcher.StartTimer (same 250 ms interval)
//   WhenAnyValue(...).Subscribe(RaisePropertyChanged) for the derived flags -> [NotifyPropertyChangedFor]
//   ReactiveCommand.Create(...) + manual property setters that recompute the readable strings -> [RelayCommand] + partial OnXxxChanged
//   ViewModelActivator Activator -> dropped (ReactiveUI type, cannot exist without ReactiveUI; same as the other ported ViewModels)
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FireAxe.Services;
using Serilog;
using System;
using System.IO;

namespace FireAxe.ViewModels;

public partial class DownloadItemViewModel : ViewModelBase, IViewModelLifecycle
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);

    private const string UnknownEtaString = "??h:??m:??s";

    private readonly IDownloadItem _download;

    private readonly string _fileName;

    private bool _isActive = false;

    /// <summary>Handle of the periodic refresh timer (was the <c>DispatcherTimer</c> disposed with the activation).</summary>
    private IDisposable? _refreshTimer = null;

    public DownloadItemViewModel(IDownloadItem downloadItem)
    {
        ArgumentNullException.ThrowIfNull(downloadItem);
        _download = downloadItem;

        try
        {
            _fileName = Path.GetFileName(_download.FilePath);
        }
        catch (Exception ex)
        {
            _fileName = "UNKNOWN";
            Log.Error(ex, "Exception occurred during getting the file name of the path: {FilePath}", _download.FilePath);
        }

        // Initial values (a partial property cannot carry an initializer).
        TotalBytesReadable = "0B";
        DownloadedBytesReadable = "0B";
        DownloadSpeedReadable = "0B/s";
        EtaReadable = UnknownEtaString;
        IsPreparing = true;

        Refresh();
    }

    public IDownloadItem DownloadItem => _download;

    public string FileName => _fileName;

    [ObservableProperty]
    public partial long TotalBytes { get; private set; }

    [ObservableProperty]
    public partial string TotalBytesReadable { get; private set; }

    partial void OnTotalBytesChanged(long value)
    {
        TotalBytesReadable = Utils.GetReadableBytes(value);
    }

    [ObservableProperty]
    public partial long DownloadedBytes { get; private set; }

    [ObservableProperty]
    public partial string DownloadedBytesReadable { get; private set; }

    partial void OnDownloadedBytesChanged(long value)
    {
        DownloadedBytesReadable = Utils.GetReadableBytes(value);
    }

    [ObservableProperty]
    public partial double DownloadSpeed { get; private set; }

    [ObservableProperty]
    public partial string DownloadSpeedReadable { get; private set; }

    partial void OnDownloadSpeedChanged(double value)
    {
        DownloadSpeedReadable = Utils.GetReadableBytes(value) + "/s";
    }

    [ObservableProperty]
    public partial double Eta { get; private set; }

    [ObservableProperty]
    public partial string EtaReadable { get; private set; }

    partial void OnEtaChanged(double value)
    {
        TimeSpan? timeSpanNullable = null;
        try
        {
            timeSpanNullable = TimeSpan.FromSeconds(value);
        }
        catch (Exception)
        {

        }
        if (timeSpanNullable == null)
        {
            EtaReadable = UnknownEtaString;
        }
        else
        {
            var timeSpan = timeSpanNullable.Value;
            double hoursDouble = timeSpan.TotalHours;
            int hours;
            if (hoursDouble > 99)
            {
                hours = 99;
            }
            else
            {
                hours = (int)hoursDouble;
            }
            int minutes = timeSpan.Minutes;
            int seconds = timeSpan.Seconds;
            EtaReadable = string.Format("{0:D2}h:{1:D2}m:{2:D2}s", hours, minutes, seconds);
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPauseable))]
    public partial bool IsPreparing { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPauseable))]
    [NotifyPropertyChangedFor(nameof(IsResumeable))]
    public partial bool IsPreparingAndPaused { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProgressAvailable))]
    [NotifyPropertyChangedFor(nameof(IsPauseable))]
    public partial bool IsRunning { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProgressAvailable))]
    [NotifyPropertyChangedFor(nameof(IsResumeable))]
    public partial bool IsPaused { get; private set; }

    [ObservableProperty]
    public partial bool IsCompleted { get; private set; }

    public bool IsProgressAvailable => IsRunning || IsPaused;

    public bool IsPauseable => (IsPreparing && !IsPreparingAndPaused) || IsRunning;

    public bool IsResumeable => IsPreparingAndPaused || IsPaused;

    [RelayCommand]
    private void Cancel()
    {
        _download.Cancel();
        Refresh();
    }

    [RelayCommand]
    private void Resume()
    {
        _download.Resume();
        Refresh();
    }

    [RelayCommand]
    private void Pause()
    {
        _download.Pause();
        Refresh();
    }

    /// <summary>
    /// Called by the View when it is attached to the visual tree
    /// (was ReactiveUI's <c>WhenActivated</c>, which started the refresh timer).
    /// </summary>
    public void Activate()
    {
        if (_isActive)
        {
            return;
        }
        _isActive = true;

        var dispatcher = AppServices.TryGetService<IUiDispatcher>();
        if (dispatcher is not null)
        {
            _refreshTimer = dispatcher.StartTimer(RefreshInterval, () =>
            {
                Refresh();
                return true;
            });
        }
    }

    /// <summary>
    /// Called by the View when it is detached or its container is recycled
    /// (was the <c>DisposeWith(disposables)</c> of the refresh timer on deactivation).
    /// </summary>
    public void Deactivate()
    {
        if (!_isActive)
        {
            return;
        }
        _isActive = false;

        Utils.DisposeAndSetNull(ref _refreshTimer);
    }

    public void Refresh()
    {
        if (IsCompleted)
        {
            return;
        }

        var status = _download.Status;

        if (IsPreparing)
        {
            if (status == DownloadStatus.Preparing)
            {
                IsPreparingAndPaused = false;
            }
            else if (status == DownloadStatus.PreparingAndPaused)
            {
                IsPreparingAndPaused = true;
            }
            else
            {
                IsPreparing = false;
                IsPreparingAndPaused = false;
                TotalBytes = _download.TotalBytes;
            }
        }
        else
        {
            DownloadedBytes = _download.DownloadedBytes;
            DownloadSpeed = _download.BytesPerSecondSpeed;
            Eta = (TotalBytes - DownloadedBytes) / DownloadSpeed;
            if (status == DownloadStatus.Running)
            {
                IsRunning = true;
                IsPaused = false;
            }
            else if (status == DownloadStatus.Paused)
            {
                IsRunning = false;
                IsPaused = true;
            }
            else
            {
                IsRunning = false;
                IsPaused = false;
                IsCompleted = true;
            }
        }
    }
}
