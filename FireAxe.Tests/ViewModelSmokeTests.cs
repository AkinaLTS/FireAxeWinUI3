// Smoke tests for the two heavy ViewModels of the P1 port (docs/PORTING_PLAN.md §3 P1):
//   MainWindowViewModel          — orchestration + Interaction -> service replacement
//   AddonNodeExplorerViewModel   — tree navigation, selection, settings round-trip
// The VMs cannot run their Avalonia view layer any more, so every external effect goes through the
// FireAxe.Services abstractions; the fakes below record the calls instead of showing windows.
using FireAxe;
using FireAxe.Services;
using FireAxe.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FireAxe.Tests;

/// <summary>Temp directory that is removed when the test finishes.</summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Join(System.IO.Path.GetTempPath(), "FireAxeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leftover temp directory must not fail the test.
        }
    }
}

internal sealed class FakeAppThemeService : IAppThemeService
{
    public AppColorTheme ColorTheme { get; set; } = AppColorTheme.Default;

    public event EventHandler? ColorThemeChanged;

    public void RaiseChanged() => ColorThemeChanged?.Invoke(this, EventArgs.Empty);
}

internal sealed class FakeDialogService : IDialogService
{
    public List<string> InfoCalls { get; } = [];

    public List<string> ErrorCalls { get; } = [];

    public List<string> ExceptionCalls { get; } = [];

    public List<string> ConfirmCalls { get; } = [];

    /// <summary>Answer returned by every ConfirmAsync call.</summary>
    public bool ConfirmResult { get; set; } = true;

    public Task<bool> ConfirmAsync(string message, string? title = null)
    {
        ConfirmCalls.Add(message);
        return Task.FromResult(ConfirmResult);
    }

    public Task ShowErrorAsync(string message, string? title = null)
    {
        ErrorCalls.Add(message);
        return Task.CompletedTask;
    }

    public Task ShowExceptionAsync(Exception exception, string? message = null, string? title = null)
    {
        ExceptionCalls.Add(message ?? exception.ToString());
        return Task.CompletedTask;
    }

    public Task ShowInfoAsync(string message, string? title = null)
    {
        InfoCalls.Add(message);
        return Task.CompletedTask;
    }

    public Task<string?> InputTextAsync(string message, string title, string defaultValue = "")
    {
        return Task.FromResult<string?>(defaultValue);
    }

    public Task<OperationInterruptReply> ShowOperationInterruptAsync(string message, string? title = null)
    {
        return Task.FromResult(OperationInterruptReply.Skip);
    }

    public Task<int> ShowChoiceAsync(string message, string? title = null, params string[] buttonTexts)
    {
        return Task.FromResult(-1);
    }
}

internal sealed class FakePickerService : IPickerService
{
    public string? NextPath { get; set; }

    public List<string?> TitlesRequested { get; } = [];

    public Task<string?> PickFolderAsync(FolderPickerOptions options)
    {
        TitlesRequested.Add(options.Title);
        return Task.FromResult(NextPath);
    }

    public Task<string?> PickFileAsync(FilePickerOptions options)
    {
        TitlesRequested.Add(options.Title);
        return Task.FromResult(NextPath);
    }

    public Task<string?> SaveFileAsync(SaveFilePickerOptions options)
    {
        TitlesRequested.Add(options.Title);
        return Task.FromResult(NextPath);
    }
}

internal sealed class FakeWindowService : IWindowService
{
    public int ActivateMainWindowCalls { get; private set; }

    public int ShowLibraryPageCalls { get; private set; }

    public Task ShowImportResultAsync(AddonRoot.ImportResult result) => Task.CompletedTask;

    public void ShowDeletionProgress(IEnumerable<(Task, string)> deletions) { }

    public void ActivateMainWindow() => ActivateMainWindowCalls++;

    public void ShowLibraryPage() => ShowLibraryPageCalls++;

    public void OpenSettingsWindow() { }

    public void OpenDownloadListWindow() { }

    public void OpenProblemListWindow() { }

    public void OpenTagManagerWindow() { }

    public void OpenVpkConflictListWindow() { }

    public void OpenReplacementOverviewWindow() { }

    public void OpenWorkshopVpkFinderWindow() { }

    public void OpenFileCleanerWindow() { }

    public void OpenAddonNameAutoSetterWindow() { }

    public void OpenFileNameFixerWindow() { }

    public void OpenAboutWindow() { }

    public void OpenNewWorkshopCollectionWindow(AddonRoot addonRoot, AddonGroup? parentGroup) { }

    public void OpenAddonTagEditorWindow(AddonNode addon) { }

    public void OpenAddonNodeCustomizeImageWindow(AddonNode addon) { }

    public Task<IReadOnlyList<AddonNode>?> PickAddonNodesAsync(AddonRoot addonRoot, bool allowMultiple, string title)
        => Task.FromResult<IReadOnlyList<AddonNode>?>(null);
}

internal sealed class FakeDownloadService : IDownloadService
{
    public IDownloadItem Download(string url, string filePath) => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class MainWindowViewModelTests
{
    private static (MainWindowViewModel Vm, FakeDialogService Dialog, TempDirectory Root) Create()
    {
        var root = new TempDirectory();
        var settings = new AppSettings(root.Path, new FakeAppThemeService());
        var dialog = new FakeDialogService();
        var vm = new MainWindowViewModel(
            settings,
            new FakeWindowService(),
            dialog,
            new FakeDownloadService(),
            new HttpClient(),
            new DownloadItemListViewModel());
        return (vm, dialog, root);
    }

    [Fact]
    public void StartsEmpty_AndExposesNoAddonRoot()
    {
        var (vm, _, root) = Create();
        using (root)
        {
            Assert.Null(vm.AddonRoot);
            Assert.True(vm.IsEmptyDirectory);
            Assert.False(vm.HasSelection);
            Assert.Equal("", vm.TitleExtraInfo);
            // Commands that need an open directory must be disabled.
            Assert.False(vm.PushCommand.CanExecute(null));
            Assert.False(vm.CloseDirectoryCommand.CanExecute(null));
            Assert.True(vm.RunGameCommand.CanExecute(null));
            // Commands that never need one stay enabled.
            Assert.True(vm.OpenSettingsWindowCommand.CanExecute(null));
            Assert.True(vm.OpenDownloadListWindowCommand.CanExecute(null));
        }
    }

    [Fact]
    public async Task OpenDirectory_CreatesAddonRootAndExplorer_CloseResets()
    {
        var (vm, _, root) = Create();
        using (root)
        {
            var dir = Path.Join(root.Path, "Root1");
            Directory.CreateDirectory(dir);

            var changed = new List<string?>();
            vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            await vm.OpenDirectoryAsync(dir);

            Assert.NotNull(vm.AddonRoot);
            Assert.Equal(dir, vm.AddonRoot!.DirectoryPath);
            Assert.NotNull(vm.AddonNodeExplorerViewModel);
            Assert.False(vm.IsEmptyDirectory);
            Assert.Contains(dir, vm.TitleExtraInfo);
            Assert.True(vm.CloseDirectoryCommand.CanExecute(null));
            // A fresh directory has no addons yet; the explorer starts at the root level.
            Assert.True(vm.AddonNodeExplorerViewModel!.IsValid);
            Assert.Null(vm.AddonNodeExplorerViewModel.CurrentGroup);
            Assert.Empty(vm.AddonNodeExplorerViewModel.CurrentNodes);
            Assert.Same(vm.AddonRoot, vm.AddonNodeExplorerViewModel.AddonRoot);

            // Derived properties must be notified in the same change set as AddonRoot.
            Assert.Contains(nameof(MainWindowViewModel.AddonRoot), changed);
            Assert.Contains(nameof(MainWindowViewModel.IsEmptyDirectory), changed);
            Assert.Contains(nameof(MainWindowViewModel.TitleExtraInfo), changed);

            vm.CloseDirectory();

            Assert.Null(vm.AddonRoot);
            Assert.Null(vm.AddonNodeExplorerViewModel);
            Assert.True(vm.IsEmptyDirectory);
            Assert.False(vm.CloseDirectoryCommand.CanExecute(null));
        }
    }

    [Fact]
    public async Task OpenDirectory_InitializesGamePath_AndKeepsSettingsChangesInSync()
    {
        var (vm, _, root) = Create();
        using (root)
        {
            var firstPath = Path.Join(root.Path, "Game1");
            vm.AppSettings.GamePath = firstPath;
            var dir = Path.Join(root.Path, "Library");
            Directory.CreateDirectory(dir);

            await vm.OpenDirectoryAsync(dir);
            Assert.NotNull(vm.AddonRoot);
            Assert.Equal(vm.AppSettings.GamePath, vm.AddonRoot.GamePath);

            var secondPath = Path.Join(root.Path, "Game2");
            vm.AppSettings.GamePath = secondPath;
            Assert.Equal(vm.AppSettings.GamePath, vm.AddonRoot.GamePath);

            vm.CloseDirectory();
            await vm.OpenDirectoryAsync(dir);
            Assert.Equal(vm.AppSettings.GamePath, vm.AddonRoot!.GamePath);
            vm.CloseDirectory();
        }
    }

    [Fact]
    public async Task HigherVersionFile_AsksConfirm_AndIsRespected()
    {
        var (vm, dialog, root) = Create();
        using (root)
        {
            var dir = Path.Join(root.Path, "HigherVersion");
            Directory.CreateDirectory(dir);
            // A version file above the running version must be reported to the user first.
            File.WriteAllText(Path.Join(dir, AddonRoot.VersionFileName), "99.0.0");

            dialog.ConfirmResult = false;
            await vm.OpenDirectoryAsync(dir);

            Assert.Single(dialog.ConfirmCalls);
            Assert.Contains("99.0.0", dialog.ConfirmCalls[0]);
            Assert.Null(vm.AddonRoot);

            // Answering yes opens the directory.
            dialog.ConfirmResult = true;
            await vm.OpenDirectoryAsync(dir);
            Assert.Equal(dir, vm.AddonRoot?.DirectoryPath);
        }
    }

    [Fact]
    public async Task AppSettingsSortAndViewSettings_RoundTripThroughExplorer()
    {
        var (vm, _, root) = Create();
        using (root)
        {
            var dir = Path.Join(root.Path, "SortSync");
            Directory.CreateDirectory(dir);
            await vm.OpenDirectoryAsync(dir);

            var explorer = vm.AddonNodeExplorerViewModel!;

            // Settings -> explorer
            vm.AppSettings.AddonNodeSortMethod = AddonNodeSortMethod.Name;
            Assert.Equal(AddonNodeSortMethod.Name, explorer.SortMethod);

            // Explorer -> settings
            explorer.SortMethod = AddonNodeSortMethod.ProblemCount;
            Assert.Equal(AddonNodeSortMethod.ProblemCount, vm.AppSettings.AddonNodeSortMethod);

            explorer.ListItemViewKind = AddonNodeListItemViewKind.MediumTile;
            Assert.Equal(AddonNodeListItemViewKind.MediumTile, vm.AppSettings.AddonNodeListItemViewKind);
            Assert.True(explorer.IsTileView);
            Assert.False(explorer.IsGridView);
        }
    }
}

public sealed class AddonNodeExplorerViewModelTests
{
    private static (AddonNodeExplorerViewModel Vm, AddonRoot Root, TempDirectory Dir) Create()
    {
        var dir = new TempDirectory();
        var root = new AddonRoot();
        root.DirectoryPath = dir.Path;
        root.LoadFile();
        return (new AddonNodeExplorerViewModel(root), root, dir);
    }

    [Fact]
    public void NewGroup_AddsNodeToCurrentGroup_AndSelectsIt()
    {
        var (vm, root, dir) = Create();
        using (dir)
        {
            int before = vm.CurrentNodes.Count;
            var created = vm.NewGroup();

            Assert.Equal(before + 1, vm.CurrentNodes.Count);
            Assert.Contains(created, vm.CurrentNodes);
            // The explorer starts at the root level (CurrentGroup == null), so a new group is a
            // direct child of the AddonRoot — AddonRoot is not an AddonGroup.
            Assert.Null(vm.CurrentGroup);
            Assert.Same(root, created.Parent);
            Assert.Same(created, Assert.Single(vm.SelectedNodes));
            Assert.True(vm.HasSelection);
            Assert.True(vm.IsSingleSelection);
            // DeleteCommand is an IAsyncRelayCommand<bool> (the bool asks "are you sure?"), so its
            // CanExecute takes the parameter instead of null.
            Assert.True(vm.DeleteCommand.CanExecute(true));
        }
    }

    [Fact]
    public void Navigation_TracksCurrentGroupAndNavBar()
    {
        var (vm, _, dir) = Create();
        using (dir)
        {
            var group = vm.NewGroup();
            group.Name = "Child";

            vm.GotoGroup(group);
            Assert.Same(group, vm.CurrentGroup);
            // The nav bar is the `Group` chain of CurrentGroup (unchanged from the Avalonia VM):
            // a top-level group has no AddonGroup ancestor, so it is a single item.
            var navBar = vm.NavBarItemViewModels.ToArray();
            Assert.Single(navBar);
            Assert.Same(group, navBar[0].AddonGroup);

            vm.GotoParent();
            Assert.Null(vm.CurrentGroup);
            Assert.Empty(vm.NavBarItemViewModels);

            // Moving to another group clears the selection, like the Avalonia VM did.
            Assert.Empty(vm.SelectedNodes);
            Assert.False(vm.HasSelection);
        }
    }

    [Fact]
    public void MovingNodes_CanBeAddedAndCleared()
    {
        var (vm, _, dir) = Create();
        using (dir)
        {
            Assert.Empty(vm.MovingNodeViewModels);

            var node = vm.NewGroup();
            vm.AddMovingNode(node);
            Assert.Single(vm.MovingNodeViewModels);

            vm.ClearMovingNodes();
            Assert.Empty(vm.MovingNodeViewModels);
        }
    }

    [Fact]
    public void ToggleOrderDirection_FlipsAscendingFlag()
    {
        var (vm, _, dir) = Create();
        using (dir)
        {
            bool before = vm.IsAscendingOrder;
            vm.ToggleOrderDirection();
            Assert.NotEqual(before, vm.IsAscendingOrder);
            vm.ToggleOrderDirection();
            Assert.Equal(before, vm.IsAscendingOrder);
        }
    }

    [Fact]
    public async Task SearchText_FiltersCurrentNodes()
    {
        var (vm, _, dir) = Create();
        using (dir)
        {
            var alpha = vm.NewGroup();
            alpha.Name = "Alpha";
            vm.GotoParent();
            var beta = vm.NewGroup();
            beta.Name = "Beta";
            vm.GotoParent();

            Assert.Equal(2, vm.CurrentNodes.Count);

            // The search is debounced (SearchThrottleTime, 0.5 s) like the ReactiveUI Throttle was,
            // and renaming a node schedules another refresh through RefreshCurrentNodesDeferred, so
            // wait for the *content* to settle instead of for a single count change.
            vm.SearchText = "Alpha";
            Assert.True(
                await WaitUntilAsync(() =>
                {
                    var nodes = vm.CurrentNodes;
                    return nodes.Count == 1 && nodes[0].Name == "Alpha";
                }),
                "the search never narrowed the list to Alpha");

            vm.ClearSearchText();
            Assert.Equal("", vm.SearchText);
            Assert.True(
                await WaitUntilAsync(() => vm.CurrentNodes.Count == 2),
                "clearing the search text did not restore both groups");
            Assert.True(vm.IsSearchTextClearable == false);
        }
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, int timeoutMs = 15000)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }
            await Task.Delay(25);
        }
        return false;
    }
}
