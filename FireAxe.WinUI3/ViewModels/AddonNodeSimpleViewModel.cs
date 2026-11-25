// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\AddonNodeSimpleViewModel.cs
// ReactiveUI -> CommunityToolkit.Mvvm: IActivatableViewModel/WhenActivated -> IViewModelLifecycle,
// ObservableAsPropertyHelper -> explicit SetProperty, Avalonia Bitmap -> IImageLoader.
using CommunityToolkit.Mvvm.Input;
using FireAxe.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;

namespace FireAxe.ViewModels;

public partial class AddonNodeSimpleViewModel : ViewModelBase, IViewModelLifecycle
{
    private static readonly TimeSpan CustomImagePathDebounce = TimeSpan.FromSeconds(0.5);

    private bool _isActive = false;

    private readonly AddonRoot _addonRoot;

    private readonly ValidRef<AddonNode> _addonRef = new();
    private bool _lastAddonPresent = false;
    private bool _disposed = false;

    /// <summary>Handlers subscribed to the current <see cref="Addon"/>; disposed when it changes.</summary>
    private AddonSubscriptions? _addonSubscriptions = null;

    private IImageSource? _image = null;
    private byte[]? _rawImage = null;

    private AddonNodeEnableState _enableState = AddonNodeEnableState.Disabled;
    private bool _shouldShowFolderIcon = false;
    private bool _shouldShowUnknownIcon = false;
    private bool _shouldShowImage = false;

    private object? _currentRefreshId = null;
    private CancellationTokenSource? _refreshCts = null;

    private readonly DebounceAction _customImagePathDebounce;

    public AddonNodeSimpleViewModel(AddonNode addon)
        : this(addon ?? throw new ArgumentNullException(nameof(addon)), null, default)
    {
    }

    public AddonNodeSimpleViewModel(AddonRoot addonRoot, Guid addonId)
        : this(null, addonRoot, addonId)
    {
    }

    public AddonNodeSimpleViewModel(AddonNode? addon, AddonRoot? addonRoot, Guid addonId)
    {
        _customImagePathDebounce = new DebounceAction(CustomImagePathDebounce, AppServices.TryGetService<IUiDispatcher>());

        if (addon is null)
        {
            ArgumentNullException.ThrowIfNull(addonRoot);
            _addonRoot = addonRoot;
            AddonId = addonId;
            if (_addonRoot.TryGetNodeById(addonId, out var node))
            {
                Addon = node;
            }
        }
        else
        {
            _addonRoot = addon.Root;
            AddonId = addon.Id;
            Addon = addon;
        }
    }

    public AddonRoot AddonRoot => _addonRoot;

    public AddonNode? Addon
    {
        get
        {
            var addon = _addonRef.TryGet();
            if (addon is null)
            {
                if (_lastAddonPresent)
                {
                    _lastAddonPresent = false;
                    OnPropertyChanged(nameof(Addon));
                }
            }
            return addon;
        }
        private set
        {
            if (value is not null)
            {
                if (value.Root != _addonRoot)
                {
                    throw new ArgumentException("Different AddonRoot instance");
                }
                if (!value.IsValid || !value.GetType().IsAssignableTo(AddonType))
                {
                    value = null;
                }
            }
            if (value == Addon)
            {
                return;
            }

            _addonRef.Set(value);
            _lastAddonPresent = value is not null;
            OnPropertyChanged();

            if (_isActive)
            {
                BindAddon(value);
            }
        }
    }

    public virtual Type AddonType => typeof(AddonNode);

    public Guid AddonId { get; }

    public IImageSource? Image
    {
        get => _image;
        private set
        {
            if (ReferenceEquals(value, _image))
            {
                return;
            }
            _image?.Dispose();
            _image = value;
            OnPropertyChanged();
            UpdateIconState();
        }
    }

    public virtual int ImageDecodingWidth => 200;

    public AddonNodeEnableState EnableState
    {
        get => _enableState;
        private set => SetProperty(ref _enableState, value);
    }

    public bool ShouldShowFolderIcon
    {
        get => _shouldShowFolderIcon;
        private set => SetProperty(ref _shouldShowFolderIcon, value);
    }

    public bool ShouldShowUnknownIcon
    {
        get => _shouldShowUnknownIcon;
        private set => SetProperty(ref _shouldShowUnknownIcon, value);
    }

    public bool ShouldShowImage
    {
        get => _shouldShowImage;
        private set => SetProperty(ref _shouldShowImage, value);
    }

    protected object? CurrentRefreshId => _currentRefreshId;

    protected bool IsActive => _isActive;

    [RelayCommand]
    private void ToggleEnabled()
    {
        if (Addon is { } addon)
        {
            addon.IsEnabled = !addon.IsEnabled;
        }
    }

    /// <summary>
    /// Called by the View when it is attached to the visual tree
    /// (was ReactiveUI's <c>WhenActivated</c>).
    /// </summary>
    public void Activate()
    {
        if (_isActive || _disposed)
        {
            return;
        }
        _isActive = true;

        if (Addon is null && _addonRoot.TryGetNodeById(AddonId, out var node))
        {
            Addon = node;
        }

        _addonRoot.NewNodeIdRegistered += OnAddonRootNewNodeIdRegistered;
        BindAddon(Addon);
        Refresh();
    }

    /// <summary>
    /// Called by the View when it is detached or its container is recycled.
    /// Releases the decoded image and every addon subscription, but keeps the
    /// <see cref="Addon"/> reference so a re-activation is cheap.
    /// </summary>
    public void Deactivate()
    {
        if (!_isActive)
        {
            return;
        }
        _isActive = false;

        _addonRoot.NewNodeIdRegistered -= OnAddonRootNewNodeIdRegistered;

        _addonSubscriptions?.Dispose();
        _addonSubscriptions = null;

        _customImagePathDebounce.Cancel();
        CancelRefreshTasks();

        Image = null;
        _rawImage = null;
        UpdateEnableState();
        UpdateIconState();
    }

    public void Refresh()
    {
        if (!_isActive)
        {
            return;
        }

        CancelRefreshTasks();
        _refreshCts = new();
        var cancellationToken = _refreshCts.Token;
        _currentRefreshId = new();
        OnRefresh(cancellationToken);
    }

    public void ClearCaches()
    {
        Addon?.ClearCaches();
        OnClearCaches();
        Refresh();
    }

    public void Check()
    {
        var addon = Addon;
        if (addon == null)
        {
            return;
        }

        if (addon is AddonGroup addonGroup)
        {
            addonGroup.CheckDescendants();
        }
        addon.Check();

        Refresh();
    }

    public void ShowInFileExplorer()
    {
        var addon = Addon;
        if (addon == null)
        {
            return;
        }

        Utils.ShowInFileExplorer(addon.FullFilePath);
    }

    /// <summary>Recomputes <see cref="EnableState"/> from the addon.</summary>
    protected void UpdateEnableState()
    {
        var addon = Addon;
        if (addon is null)
        {
            EnableState = AddonNodeEnableState.Disabled;
            return;
        }

        EnableState = addon.IsEnabled
            ? (addon.IsEnabledInHierarchy ? AddonNodeEnableState.Enabled : AddonNodeEnableState.EnabledSuppressed)
            : AddonNodeEnableState.Disabled;
    }

    /// <summary>Recomputes the three mutually exclusive icon/image flags.</summary>
    protected void UpdateIconState()
    {
        var addon = Addon;
        bool hasImage = Image is not null;
        bool isDefaultIconFolder = addon is not null && IsAddonDefaultIconFolder(addon);

        ShouldShowImage = addon is not null && hasImage;
        ShouldShowUnknownIcon = addon is not null && !hasImage && !isDefaultIconFolder;
        ShouldShowFolderIcon = addon is not null && !hasImage && isDefaultIconFolder;

        static bool IsAddonDefaultIconFolder(AddonNode node) =>
            node is AddonGroup || (node is RefAddonNode refAddon && refAddon.ActualSourceAddon is AddonGroup);
    }

    protected virtual void OnNewAddon(AddonNode addon, AddonSubscriptions subscriptions)
    {
    }

    protected virtual void OnNullAddon()
    {
    }

    protected virtual void OnRefresh(CancellationToken cancellationToken)
    {
        var addon = Addon;
        var refreshId = CurrentRefreshId;

        RefreshImage();

        async void RefreshImage()
        {
            Image = null;
            _rawImage = null;

            if (addon == null)
            {
                return;
            }

            byte[]? imageData = null;

            try
            {
                var customImagePath = addon.ActualImageFilePath;
                if (customImagePath != null)
                {
                    try
                    {
                        if (File.Exists(customImagePath))
                        {
                            imageData = await File.ReadAllBytesAsync(customImagePath, cancellationToken);
                            if (refreshId != CurrentRefreshId)
                            {
                                return;
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Exception occurred during reading custom image: {FilePath}", customImagePath);
                    }
                }

                if (imageData == null)
                {
                    imageData = await addon.GetImageAllowCacheAsync(cancellationToken);
                    if (refreshId != CurrentRefreshId)
                    {
                        return;
                    }
                    _rawImage = imageData;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during getting image for {AddonName}.", addon.Name);
                return;
            }

            if (imageData == null)
            {
                return;
            }

            var imageLoader = AppServices.TryGetService<IImageLoader>();
            if (imageLoader is null)
            {
                return;
            }

            IImageSource? decoded = null;
            try
            {
                decoded = await imageLoader.DecodeToWidthAsync(imageData, ImageDecodingWidth, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during decoding the image of {AddonName}.", addon.Name);
                return;
            }

            if (decoded is null || refreshId != CurrentRefreshId || !_isActive)
            {
                decoded?.Dispose();
                return;
            }

            SetImageOnUiThread(decoded, refreshId);
        }
    }

    protected virtual void OnClearCaches()
    {
        Image = null;
    }

    private void SetImageOnUiThread(IImageSource image, object? refreshId)
    {
        void Assign()
        {
            // Dispatch can outlive an unload, recycle, or newer refresh. Recheck when it runs.
            if (!_isActive || refreshId != CurrentRefreshId)
            {
                image.Dispose();
                return;
            }
            Image = image;
        }
        if (AppServices.TryGetService<IUiDispatcher>() is not { } dispatcher || dispatcher.HasThreadAccess)
        {
            Assign();
            return;
        }
        dispatcher.Post(Assign);
    }

    private void OnAddonRootNewNodeIdRegistered(AddonNode addon)
    {
        if (Addon != null)
        {
            return;
        }

        if (addon.Id == AddonId)
        {
            Addon = addon;
        }
    }

    /// <summary>Rebinds every subscription to <paramref name="addon"/> (or tears them down for null).</summary>
    private void BindAddon(AddonNode? addon)
    {
        _addonSubscriptions?.Dispose();
        _addonSubscriptions = null;

        if (addon is null)
        {
            UpdateEnableState();
            UpdateIconState();
            OnNullAddon();
            return;
        }

        var subscriptions = new AddonSubscriptions(addon);
        _addonSubscriptions = subscriptions;

        subscriptions.Add(addon, nameof(AddonNode.IsValid), OnAddonIsValidChanged);
        subscriptions.Add(addon, nameof(AddonNode.Id), OnAddonIdChanged);
        subscriptions.Add(addon, nameof(AddonNode.IsEnabled), OnAddonEnableStateChanged);
        subscriptions.Add(addon, nameof(AddonNode.IsEnabledInHierarchy), OnAddonEnableStateChanged);
        subscriptions.Add(addon, nameof(AddonNode.CustomImagePath), OnAddonCustomImagePathChanged);

        addon.ImageChanged += OnAddonImageChanged;
        subscriptions.Add(() => addon.ImageChanged -= OnAddonImageChanged);

        UpdateEnableState();
        UpdateIconState();
        OnNewAddon(addon, subscriptions);
    }

    private void OnAddonIsValidChanged()
    {
        if (Addon is { IsValid: false })
        {
            Addon = null;
        }
    }

    private void OnAddonIdChanged()
    {
        if (Addon is { } addon && addon.Id != AddonId)
        {
            Addon = null;
        }
    }

    private void OnAddonEnableStateChanged()
    {
        UpdateEnableState();
    }

    private void OnAddonCustomImagePathChanged()
    {
        _customImagePathDebounce.Run(Refresh);
    }

    private void OnAddonImageChanged()
    {
        Refresh();
    }

    private void CancelRefreshTasks()
    {
        if (_refreshCts != null)
        {
            _refreshCts.Cancel();
            _refreshCts.Dispose();
            _refreshCts = null;
        }
    }

    /// <summary>
    /// Bundle of subscriptions to one addon, so the whole set can be released at once
    /// (replaces the per-addon <c>CompositeDisposable</c>).
    /// </summary>
    protected sealed class AddonSubscriptions : IDisposable
    {
        private readonly AddonNode _addon;
        private readonly List<Action> _unsubscribers = [];
        private readonly Dictionary<string, Action> _propertyHandlers = [];
        private bool _disposed = false;

        internal AddonSubscriptions(AddonNode addon)
        {
            _addon = addon;

            void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName is { } name && _propertyHandlers.TryGetValue(name, out var handler))
                {
                    handler();
                }
            }

            _addon.PropertyChanged += OnPropertyChanged;
            _unsubscribers.Add(() => _addon.PropertyChanged -= OnPropertyChanged);
        }

        internal void Add(AddonNode addon, string propertyName, Action handler)
        {
            ArgumentNullException.ThrowIfNull(addon);
            ArgumentNullException.ThrowIfNull(propertyName);
            ArgumentNullException.ThrowIfNull(handler);

            _propertyHandlers[propertyName] = handler;
        }

        internal void Add(Action unsubscribe)
        {
            _unsubscribers.Add(unsubscribe);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            _propertyHandlers.Clear();
            foreach (var unsubscribe in _unsubscribers)
            {
                unsubscribe();
            }
            _unsubscribers.Clear();
        }
    }
}
