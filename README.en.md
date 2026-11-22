![FireAxe Logo](FireAxe.WinUI3/Assets/AppLogo.ico)
# FireAxe (WinUI 3)

**[简体中文](README.md)** | **English**

FireAxe is an open-source GUI tool for managing *Left 4 Dead 2* addons. This repository is a **WinUI 3 rewrite** of `FireAxe_Alt` (the Avalonia version, v0.7.6): same addon-management semantics, with a native Windows UI.

Built with WinUI 3, .NET 10, Windows App SDK 2.6.5 and CommunityToolkit.Mvvm 8.4.2.

> **Status: 0.1.0 developer preview.** The main view, tool pages, native navigation, categorized settings and the packaging script are in place, and the business-layer tests pass. Visual/interactive sign-off on real hardware and a clean-machine launch check are still open, so there is no formal release yet. See the [porting plan](docs/PORTING_PLAN.md) (Chinese) and the [phase-notes index](docs/phase-notes/README.md) (Chinese) for progress and open items.

## Features

Same as FireAxe_Alt:

- Organize addons hierarchically through groups, with reference addons for switching play schemes
- Download Steam Workshop items and collections
- Enablement state, priority, dependency and tag management
- VPK conflict detection and addon problem checks
- Local VPK file management, VPK and Workshop item details
- Addon import/export (`.addonroot`), search and filtering

Differences in the WinUI 3 version:

- One native main window: secondary screens became in-window page navigation instead of separate top-level windows
- WinUI 3 styled categorized settings and native controls
- Virtualized data-heavy lists so large libraries stay responsive

## Using a release

Requires **Windows 10 2004 (19041) or later / Windows 11, x64**.

1. **Extract the whole** `FireAxe-<version>-win-x64.zip` into a directory you intend to keep.
2. Run `FireAxe.exe`. This is a folder deployment, not a single-file executable — move the entire folder when relocating it.
3. The extracted folder already carries the .NET and Windows App SDK runtimes; no extra installation is required.

Settings and logs live in `Documents/FireAxe` by default, with logs under its `Logs` subdirectory. On first run, set your addon directory and game directory in settings.

Pushing enablement state writes into the game directory and creates symbolic links: enable Windows **Developer Mode**. If policy blocks it, close the app and relaunch it as administrator.

## Building from source

You need Windows, the .NET SDK **10.0.4xx**, PowerShell 7 and git-lfs, plus Windows App Runtime 2.6 installed locally (development builds start framework-dependent).

```powershell
git lfs pull
dotnet build FireAxe.sln -c Debug
.\FireAxe.WinUI3\bin\x64\Debug\net10.0-windows10.0.26100.0\FireAxe.exe
dotnet test FireAxe.Tests\FireAxe.Tests.csproj -c Debug -p:Platform=x64
```

The application project pins `Platform=x64`, so both building the project and building the solution write to `bin/x64/<configuration>/...`.

To produce the distribution zip:

```powershell
pwsh -File .\scripts\Publish-Windows.ps1
```

The script runs a Release build and tests, publishes `win-x64` with both .NET and Windows App SDK self-contained, collects project and NuGet license files and generates NOTICE, checks static native dependencies and the application resource (PRI) contents, then writes the zip, SHA-256 and a publish manifest. Every run uses its own directory (default `artifacts/publish`) and keeps the raw evidence in that run's `evidence` folder. The script never launches the app.

## FAQ

**Why can't I see my addons in the game?** Make sure the addon and every group containing it are enabled, and that you pressed push. If the game was already running, refresh the addon list in the game menu after pushing.

**How do I migrate Workshop addons?** Use the Workshop VPK finder under the tools menu, then unsubscribe from them on Steam — FireAxe does not manage subscribed Workshop addons.

**Push fails — what now?** Make sure the file system is NTFS (or another type that supports symbolic links) and that the game directory is writable; running as administrator may help.

**How does priority work?** For files with identical names, the highest priority wins. Each addon has its own priority, and the priorities of the groups containing it add up to a global priority; the global priority is what takes effect.

**Can I run multiple instances?** One instance per data directory — a second launch is redirected to the running window. Point `FIREAXE_DATA_DIRECTORY` at another directory to run a parallel profile:

```powershell
$env:FIREAXE_DATA_DIRECTORY = 'D:\FireAxe-TestProfile'
.\FireAxe.WinUI3\bin\x64\Debug\net10.0-windows10.0.26100.0\FireAxe.exe
```

**Keyboard shortcuts in the addon list:**

| Key | Action |
|---|---|
| Ctrl+F | Focus search |
| Ctrl+X / Ctrl+V | Mark for move / move here |
| Delete | Delete selection |
| F2 | Rename the single selected item |
| Enter / Esc | Commit / cancel the rename editor |

## Repository layout

- `FireAxe.Core` — business logic and file formats, UI-independent.
- `FireAxe.WinUI3` — single main window, views, view models and platform services.
- `FireAxe.Tests` — business, view-model and regression tests.
- `scripts` — the packaging scripts shared by local use and CI (`.github/workflows/build-and-release.yml`).
- `docs` — [porting plan](docs/PORTING_PLAN.md), [phase-notes index](docs/phase-notes/README.md), [native UI design](docs/P6_NATIVE_UI_DESIGN.md) (all in Chinese).

`FireAxe_Alt` is the frozen Avalonia baseline, kept read-only for comparison; all fixes land in this repository. Namespaces stay `FireAxe` / `FireAxe.Views` / `FireAxe.ViewModels`.

On branches and pull requests CI only builds, tests and uploads artifacts; a release is created only by pushing a `v<version>` tag matching `Directory.Build.props`, or by a manual dispatch that names an existing tag and sets `create_release`.

## License

Apache-2.0, see [LICENSE.txt](LICENSE.txt). The distribution zip ships the dependency list plus the original third-party license files.
