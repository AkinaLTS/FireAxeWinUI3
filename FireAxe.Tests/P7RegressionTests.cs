// Source: original P7 regression coverage for geometry, isolated profiles, push and large libraries.
using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using FireAxe.ViewModels;
using Xunit.Abstractions;

namespace FireAxe.Tests;

public sealed class P7RegressionTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(1, 1145, 700)]
    [InlineData(1.25, 1431, 875)]
    [InlineData(1.5, 1718, 1050)]
    [InlineData(2, 2290, 1400)]
    public void ClientDips_ConvertToPhysicalPixels(double scale, int width, int height)
    {
        Assert.Equal((width, height), WindowGeometry.GetClientSize(null, null, scale, 4000, 3000));
    }

    [Fact]
    public void CorruptAndOversizedGeometry_StaysInsideWorkArea()
    {
        Assert.Equal((1145, 700), WindowGeometry.GetClientSize(double.NaN, double.PositiveInfinity, double.NaN, 1920, 1080));
        Assert.Equal((1280, 720), WindowGeometry.GetClientSize(double.MaxValue, double.MaxValue, 2, 1280, 720));
        Assert.Equal((640, 480), WindowGeometry.GetClientSize(1, 1, 1, 1920, 1080));
        Assert.Equal((320, 240), WindowGeometry.GetClientSize(1145, 700, 2, 320, 240));
    }

    [Fact]
    public void ProfileOverride_RequiresAbsolutePathAndNormalizesIt()
    {
        Assert.Throws<ArgumentException>(() => StartupPaths.ResolveDataDirectory("relative-profile"));
        using var directory = new TempDirectory();
        Assert.Equal(directory.Path, StartupPaths.ResolveDataDirectory(Path.Join(directory.Path, "child", "..")));
        Assert.Equal(Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FireAxe"),
            StartupPaths.ResolveDataDirectory(null));
    }

    [Fact]
    public void SymbolicLinkProbe_LeavesNoFilesAndPushUsesTemporaryGameOnly()
    {
        using var directory = new TempDirectory();
        var game = Path.Join(directory.Path, "Game");
        var addons = GamePathUtils.GetAddonsPath(game);
        Directory.CreateDirectory(addons);
        // This deliberately fails if the test machine lacks symlink privilege. Never weaken the assertion.
        SymbolicLinkPreflight.Verify(addons);
        Assert.Empty(Directory.EnumerateFileSystemEntries(addons));
        var root = new AddonRoot { DirectoryPath = Path.Join(directory.Path, "Library"), GamePath = game };
        Directory.CreateDirectory(root.DirectoryPath);
        root.LoadFile();
        var target = Path.Join(root.DirectoryPath, "source.vpk");
        File.WriteAllText(target, "temporary link target; no VPK parsing is performed");
        var oldLink = Path.Join(addons, "fireaxe_link_1.vpk");
        File.CreateSymbolicLink(oldLink, target);
        File.WriteAllText(Path.Join(addons, "unmanaged.vpk"), "keep");
        root.Push();
        Assert.Equal("keep", File.ReadAllText(Path.Join(addons, "unmanaged.vpk")));
        Assert.True(File.Exists(target));
        Assert.False(File.Exists(oldLink));
        Assert.True(File.Exists(GamePathUtils.GetAddonListPath(game)));
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(addons), p => Path.GetFileName(p).StartsWith("fireaxe_probe_"));
        output.WriteLine("BCL symlink preflight and AddonRoot.Push succeeded in an isolated temporary game directory.");
    }

    [Fact]
    public async Task TwoThousandNodes_SortSearchAndSaveReload()
    {
        using var directory = new TempDirectory();
        var root = new AddonRoot { DirectoryPath = directory.Path };
        root.LoadFile();
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 2000; i++)
            AddonNode.Create<AddonGroup>(root).Name = $"Node{i:D4}";
        output.WriteLine($"Create 2000 groups: {watch.ElapsedMilliseconds} ms");
        using (var explorer = new AddonNodeExplorerViewModel(root))
        {
            Assert.Equal(2000, explorer.CurrentNodes.Count);
            watch.Restart();
            explorer.SortMethod = AddonNodeSortMethod.Name;
            explorer.IsAscendingOrder = true;
            Assert.Equal("Node0000", explorer.CurrentNodes[0].Name);
            explorer.IsAscendingOrder = false;
            Assert.Equal("Node1999", explorer.CurrentNodes[0].Name);
            output.WriteLine($"Sort ascending + descending: {watch.ElapsedMilliseconds} ms");
            watch.Restart();
            explorer.SearchText = "Node1999";
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline && (explorer.IsSearching || explorer.CurrentNodes.Count != 1 || explorer.CurrentNodes[0].Name != "Node1999"))
                await Task.Delay(25);
            Assert.Equal("Node1999", Assert.Single(explorer.CurrentNodes).Name);
            output.WriteLine($"Search including debounce: {watch.ElapsedMilliseconds} ms");
        }
        watch.Restart();
        root.Save();
        var reloaded = new AddonRoot { DirectoryPath = directory.Path };
        reloaded.LoadFile();
        Assert.Equal(2000, reloaded.Nodes.Count);
        Assert.Contains(reloaded.Nodes, node => node.Name == "Node1999");
        output.WriteLine($"Save + reload 2000 groups: {watch.ElapsedMilliseconds} ms (data path only; no UI rendering measured)");
    }

    [Fact]
    public void AddonRootDeserialization_AllowsKnownSaveTypes_AndRejectsArbitraryOrAbstractTypes()
    {
        using var directory = new TempDirectory();
        var root = new AddonRoot { DirectoryPath = directory.Path };
        root.LoadFile();
        var group = AddonNode.Create<AddonGroup>(root);
        group.Name = "G1";
        var refNode = AddonNode.Create<RefAddonNode>(root);
        refNode.Name = "R1";

        string json = AddonRoot.Serialize(root.CreateSave());
        var roundTripped = AddonRoot.Deserialize(json);
        Assert.Equal(2, roundTripped.Nodes.Length);
        Assert.Contains(roundTripped.Nodes, n => n is AddonGroupSave);
        Assert.Contains(roundTripped.Nodes, n => n is RefAddonNodeSave);

        const string maliciousJson = """
        {
          "Nodes": [
            {
              "$type": "System.Diagnostics.Process, System.Diagnostics.Process"
            }
          ]
        }
        """;
        Assert.Throws<AddonRootDeserializationException>(() => AddonRoot.Deserialize(maliciousJson));

        const string abstractSaveJson = """
        {
          "Nodes": [
            {
              "$type": "FireAxe.VpkAddonSave, FireAxe.Core"
            }
          ]
        }
        """;
        Assert.Throws<AddonRootDeserializationException>(() => AddonRoot.Deserialize(abstractSaveJson));
    }

    [Theory]
    [InlineData("../outside.vpk")]
    [InlineData("..\\outside.vpk")]
    [InlineData("sub/addon.vpk")]
    [InlineData("sub\\addon.vpk")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("C:\\outside.vpk")]
    public void WorkshopVpkMetaInfo_RejectsPathTraversalInCurrentFile(string invalidCurrentFile)
    {
        Assert.Throws<ArgumentException>(() => new WorkshopVpkMetaInfo
        {
            PublishedFileId = 123456,
            TimeUpdated = 1,
            CurrentFile = invalidCurrentFile
        });
    }

    [Fact]
    public void WorkshopVpkMetaInfo_AcceptsSingleFileName()
    {
        var info = new WorkshopVpkMetaInfo
        {
            PublishedFileId = 123456,
            TimeUpdated = 1,
            CurrentFile = "123456.vpk"
        };
        Assert.Equal("123456.vpk", info.CurrentFile);
    }

    [Theory]
    [InlineData("../outside.png")]
    [InlineData("a/../../outside.png")]
    [InlineData("a\\..\\..\\outside.png")]
    [InlineData("./../outside.png")]
    [InlineData("C:\\outside.png")]
    public void AddonNodeCustomImagePath_RejectsTraversalOutsideAddonRoot(string invalidPath)
    {
        using var directory = new TempDirectory();
        var root = new AddonRoot { DirectoryPath = directory.Path };
        root.LoadFile();
        var group = AddonNode.Create<AddonGroup>(root);

        Assert.Throws<FileOutOfAddonRootException>(() => group.CustomImagePath = invalidPath);
    }

    [Fact]
    public void AddonNodeCustomImagePath_AcceptsRelativePathInsideAddonRoot()
    {
        using var directory = new TempDirectory();
        var root = new AddonRoot { DirectoryPath = directory.Path };
        root.LoadFile();
        var group = AddonNode.Create<AddonGroup>(root);

        group.CustomImagePath = "images/preview.png";
        Assert.Equal("images/preview.png", group.CustomImagePath);
        Assert.Equal(Path.Join(directory.Path, "images/preview.png"), group.CustomImageFullPath);
    }

    [Fact]
    public void WriteFileAtomically_PreservesOriginalContentOnFailure_AndLeavesNoTempFiles()
    {
        using var directory = new TempDirectory();
        string targetPath = Path.Join(directory.Path, "data.txt");

        FileSystemUtils.WriteAllTextAtomically(targetPath, "initial");
        Assert.Equal("initial", File.ReadAllText(targetPath));
        Assert.Single(Directory.GetFiles(directory.Path));

        Assert.Throws<IOException>(() => FileSystemUtils.WriteFileAtomically(targetPath, stream =>
        {
            stream.Write("partial"u8);
            throw new IOException("simulated crash");
        }));

        Assert.Equal("initial", File.ReadAllText(targetPath));
        Assert.Single(Directory.GetFiles(directory.Path));
    }

    [Fact]
    public async Task AddonNodeJumpMessage_SwitchesBackToLibraryPage()
    {
        using var directory = new TempDirectory();
        var settings = new AppSettings(directory.Path, new FakeAppThemeService());
        var windowService = new FakeWindowService();
        using var vm = new MainWindowViewModel(
            settings,
            windowService,
            new FakeDialogService(),
            new FakeDownloadService(),
            new HttpClient(),
            new DownloadItemListViewModel());

        var libraryDir = Path.Join(directory.Path, "Library");
        Directory.CreateDirectory(libraryDir);
        await vm.OpenDirectoryAsync(libraryDir);
        var group = AddonNode.Create<AddonGroup>(vm.AddonRoot!);
        group.Name = "JumpTarget";

        WeakReferenceMessenger.Default.Send(new AddonNodeJumpMessage(group));
        Assert.Equal(1, windowService.ShowLibraryPageCalls);
    }

    [Fact]
    public void TestBuild_DoesNotInjectWasdkBootstrapModuleInitializer()
    {
        var appAssembly = typeof(App).Assembly;
        Assert.Null(appAssembly.GetType("Microsoft.Windows.ApplicationModel.WindowsAppRuntime.Common.AutoInitialize"));
        Assert.Null(appAssembly.GetType("Microsoft.Windows.ApplicationModel.DynamicDependency.BootstrapCS.AutoInitialize"));
    }
}
