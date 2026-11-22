// Ported from FireAxe_Alt\FireAxe.GUI\SaveManager.cs
//   Avalonia DispatcherTimer(interval, priority, callback) -> DispatcherQueueTimer via IUiDispatcher.StartTimer
//   App.ShutdownRequested += SaveAll(force)               -> App calls SaveAll(true) from its shutdown sequence
using System;
using System.Collections.Generic;
using FireAxe.Services;
using Serilog;

namespace FireAxe;

/// <summary>
/// Periodically saves every registered <see cref="ISaveable"/> whose <c>RequestSave</c> flag is set.
/// </summary>
public sealed class SaveManager : IDisposable
{
    public static readonly TimeSpan DefaultAutoSaveInterval = TimeSpan.FromSeconds(3);

    private readonly IUiDispatcher _dispatcher;

    private readonly List<ISaveable> _saveables = [];

    private IDisposable? _autoSaveTimer = null;

    private bool _disposed = false;

    private bool _active = false;

    public SaveManager(IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
    }

    public IEnumerable<ISaveable> Saveables => _saveables;

    public TimeSpan AutoSaveInterval { get; set; } = DefaultAutoSaveInterval;

    public void Run()
    {
        if (_active)
        {
            return;
        }

        _autoSaveTimer = _dispatcher.StartTimer(AutoSaveInterval, () =>
        {
            SaveAll();
            return true;
        });
        _active = true;
    }

    public void Register(ISaveable saveable)
    {
        ArgumentNullException.ThrowIfNull(saveable);
        _saveables.Add(saveable);
    }

    public void SaveAll(bool forceSave = false)
    {
        foreach (var saveable in _saveables)
        {
            if (forceSave || saveable.RequestSave)
            {
                try
                {
                    saveable.Save();
                    saveable.RequestSave = false;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception occurred during ISaveable.Save. (ClassName: {ClassName})", saveable.GetType().FullName);
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _autoSaveTimer?.Dispose();
        _autoSaveTimer = null;
        _disposed = true;
    }
}
