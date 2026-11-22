# Source: original P7 implementation for FireAxeWinUI3.
# Uses .NET publish and Windows App SDK's built-in self-contained deployment:
# https://learn.microsoft.com/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps
# Requires PowerShell 7 and the .NET 10 SDK on Windows. No application process is launched.
<#
.SYNOPSIS
Build, test and publish an unpackaged Windows x64 zip without uploading anything.
.EXAMPLE
pwsh -File ./scripts/Publish-Windows.ps1
.EXAMPLE
pwsh -File ./scripts/Publish-Windows.ps1 -ArtifactRoot artifacts/ci -ExpectedVersion 0.1.0
.DESCRIPTION
Each invocation creates a new directory and preserves previous files. The returned object
contains ZipPath, ChecksumPath, PublishDirectory, EvidenceDirectory and ManifestPath.
-SkipTests is for local iteration; CI always uses the default build and test steps.
Extract the entire zip before running FireAxe.exe; this is a folder deployment, not a single file.
#>
[CmdletBinding()]
param(
    [string] $ArtifactRoot = 'artifacts/publish',
    [string] $ExpectedVersion,
    [switch] $SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Publishing WinUI 3 requires Windows and PowerShell 7.' }
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repositoryRoot 'FireAxe.WinUI3/FireAxe.WinUI3.csproj'
[xml] $versionProps = Get-Content (Join-Path $repositoryRoot 'Directory.Build.props') -Raw
$version = [string] $versionProps.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw "Unsupported release version: '$version'."
}
if ($ExpectedVersion -and $ExpectedVersion -cne $version) {
    throw "Release version '$ExpectedVersion' does not match Directory.Build.props '$version'."
}
$artifactName = "FireAxe-$version-win-x64"
$root = [IO.Path]::GetFullPath($ArtifactRoot, $repositoryRoot)
$runDirectory = Join-Path $root "$artifactName-$([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
$publishDirectory = Join-Path $runDirectory 'publish'
$evidenceDirectory = Join-Path $runDirectory 'evidence'
New-Item -ItemType Directory -Path $publishDirectory, $evidenceDirectory | Out-Null

function Invoke-DotNet {
    param([string] $Name, [string[]] $Arguments)
    $logPath = Join-Path $evidenceDirectory "$Name.txt"
    "dotnet $($Arguments -join ' ')" | Set-Content $logPath -Encoding utf8
    & dotnet @Arguments 2>&1 | Tee-Object -FilePath $logPath -Append | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet $Name failed ($LASTEXITCODE); see $logPath" }
}

Push-Location $repositoryRoot
try {
    Invoke-DotNet 'sdk' @('--info')
    if (-not $SkipTests) {
        Invoke-DotNet 'build' @('build', 'FireAxe.sln', '-c', 'Release', '-p:Platform=x64', '--nologo')
        Invoke-DotNet 'test' @('test', 'FireAxe.Tests/FireAxe.Tests.csproj', '-c', 'Release', '-p:Platform=x64', '--no-build', '--logger', 'trx;LogFileName=tests.trx', '--results-directory', $evidenceDirectory, '--nologo')
    }
    # Keep WinUI native runtime files beside the exe. SDK auto-initialization selects
    # UndockedRegFreeWinRT for this unpackaged self-contained build.
    Invoke-DotNet 'publish' @('publish', $project, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-p:WindowsAppSDKSelfContained=true', '-p:WindowsPackageType=None', '-p:PublishSingleFile=false', '-p:PublishTrimmed=false', '-p:DebugType=None', '--nologo', '-o', $publishDirectory)

    Copy-Item (Join-Path $repositoryRoot 'LICENSE.txt') (Join-Path $publishDirectory 'LICENSE.txt')
    # Preserve package-supplied license and notice files, including the Windows App SDK
    # and .NET runtime packs. NuGet's assets file is the source of package identities.
    $assets = Get-Content (Join-Path $repositoryRoot 'FireAxe.WinUI3/obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
    $licensesDirectory = Join-Path $publishDirectory 'licenses'
    New-Item -ItemType Directory -Path $licensesDirectory | Out-Null
    $packageRecords = [Collections.Generic.List[object]]::new()
    $packageIdentities = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $assets.libraries.GetEnumerator()) {
        if ($entry.Value.type -eq 'package') { [void] $packageIdentities.Add($entry.Key) }
    }
    # Runtime packs can be downloadDependencies rather than libraries in project.assets.json.
    # Use NuGet's own range parser from the selected SDK, including [x, x] exact ranges.
    $sdkVersion = & dotnet --version
    if ($LASTEXITCODE -ne 0) { throw 'Cannot determine the selected .NET SDK.' }
    $dotnetRoot = Split-Path (Get-Command dotnet).Source -Parent
    Add-Type -Path (Join-Path $dotnetRoot "sdk/$sdkVersion/NuGet.Versioning.dll")
    foreach ($framework in $assets.project.frameworks.Values) {
        if ($framework.ContainsKey('downloadDependencies')) {
            foreach ($dependency in $framework.downloadDependencies) {
                $range = [NuGet.Versioning.VersionRange]::Parse($dependency.version)
                if (-not $range.IsMinInclusive -or -not $range.IsMaxInclusive -or $range.MinVersion -ne $range.MaxVersion) {
                    throw "Runtime download dependency must have an exact version: $($dependency.name) $range"
                }
                [void] $packageIdentities.Add("$($dependency.name)/$($range.MinVersion.ToNormalizedString())")
            }
        }
    }
    foreach ($identity in ($packageIdentities | Sort-Object)) {
        $parts = $identity.Split('/')
        $packageDirectory = $null
        foreach ($packageRoot in $assets.packageFolders.Keys) {
            $candidate = Join-Path $packageRoot $identity.ToLowerInvariant()
            if (Test-Path $candidate -PathType Container) { $packageDirectory = $candidate; break }
        }
        if (-not $packageDirectory) { throw "Restored package directory is missing: $identity" }
        $nuspecPath = Get-ChildItem $packageDirectory -Filter '*.nuspec' -File | Select-Object -First 1
        if (-not $nuspecPath) { throw "NuGet metadata is missing: $identity" }
        [xml] $nuspec = Get-Content $nuspecPath.FullName -Raw
        $metadata = $nuspec.SelectSingleNode('/*[local-name()="package"]/*[local-name()="metadata"]')
        $licenseNode = $metadata.SelectSingleNode('*[local-name()="license"]')
        $licenseUrlNode = $metadata.SelectSingleNode('*[local-name()="licenseUrl"]')
        $licenseValue = if ($licenseNode) { $licenseNode.InnerText } else { '' }
        $licenseUrl = if ($licenseUrlNode) { $licenseUrlNode.InnerText } else { '' }
        $licenseFiles = @(Get-ChildItem $packageDirectory -File | Where-Object Name -Match '(?i)^(licen[sc]e|notice|third[-_ ]?party)([._ -]|$)')
        if ($licenseNode -and $licenseNode.GetAttribute('type') -eq 'file') {
            $declaredFile = Join-Path $packageDirectory $licenseValue
            if (-not (Test-Path $declaredFile -PathType Leaf)) { throw "Declared license is missing: $identity / $licenseValue" }
            $licenseFiles += Get-Item $declaredFile
        }
        $copied = [Collections.Generic.List[string]]::new()
        foreach ($file in ($licenseFiles | Sort-Object FullName -Unique)) {
            $relative = [IO.Path]::GetRelativePath($packageDirectory, $file.FullName)
            $destination = Join-Path $licensesDirectory "$($parts[0])/$($parts[1])/$relative"
            New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
            Copy-Item $file.FullName $destination
            $copied.Add([IO.Path]::GetRelativePath($publishDirectory, $destination).Replace('\', '/'))
        }
        $packageRecords.Add([ordered]@{ package = $identity; license = $licenseValue; licenseUrl = $licenseUrl; files = @($copied) })
    }
    $notices = [Collections.Generic.List[string]]::new()
    $notices.Add('Source: generated by scripts/Publish-Windows.ps1 from restored NuGet package metadata.')
    $notices.Add('Dependency inventory includes runtime and build-time packages; original notices are under licenses/.')
    foreach ($record in $packageRecords) {
        $notices.Add("`n$($record.package)`nLicense: $($record.license)`nLicense URL: $($record.licenseUrl)`nFiles: $($record.files -join ', ')")
    }
    $notices | Set-Content (Join-Path $publishDirectory 'THIRD-PARTY-NOTICES.txt') -Encoding utf8

    $requiredFiles = @('FireAxe.exe', 'FireAxe.dll', 'FireAxe.deps.json', 'FireAxe.runtimeconfig.json', 'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll', 'Microsoft.UI.Xaml.dll', 'Microsoft.WindowsAppRuntime.dll', 'FireAxe.pri', 'Assets/AppLogo.ico', 'LICENSE.txt', 'THIRD-PARTY-NOTICES.txt')
    foreach ($relative in $requiredFiles) {
        if (-not (Test-Path (Join-Path $publishDirectory $relative) -PathType Leaf)) { throw "Publish dependency is missing: $relative" }
    }
    # Release embeds compiled XAML in the PRI. Validate the SDK resource map instead
    # of requiring loose .xbf files (which are a Debug layout detail).
    $sdkToolsIdentity = @($packageRecords | Where-Object { $_.package.StartsWith('Microsoft.Windows.SDK.BuildTools/', [StringComparison]::OrdinalIgnoreCase) })[0].package
    $sdkToolsPath = $null
    foreach ($packageRoot in $assets.packageFolders.Keys) {
        $candidate = Join-Path $packageRoot $assets.libraries[$sdkToolsIdentity].path
        if (Test-Path $candidate -PathType Container) { $sdkToolsPath = $candidate; break }
    }
    if (-not $sdkToolsPath) { throw "Restored Windows SDK BuildTools directory is missing: $sdkToolsIdentity" }
    $makePri = @(Get-ChildItem (Join-Path $sdkToolsPath 'bin') -Filter makepri.exe -Recurse | Where-Object { $_.Directory.Name -eq 'x64' })[0].FullName
    $priDumpPath = Join-Path $evidenceDirectory 'app-pri.xml'
    & $makePri dump /if (Join-Path $publishDirectory 'FireAxe.pri') /of $priDumpPath /dt detailed /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'The Windows SDK could not read the application PRI.' }
    [xml] $priDump = Get-Content $priDumpPath -Raw
    $xamlResources = @('Files/App.xbf', 'Files/Views/MainWindow.xbf', 'Files/Views/AddonNodeExplorerView.xbf')
    foreach ($resource in $xamlResources) {
        $uri = "ms-resource://FireAxe/$resource"
        $record = $priDump.SelectSingleNode("//NamedResource[@uri='$uri']/Candidate[@type='EmbeddedData']/Base64Value")
        if ($null -eq $record -or [string]::IsNullOrEmpty($record.InnerText)) { throw "Embedded XAML is missing from PRI: $resource" }
    }
    $xamlResources | Set-Content (Join-Path $evidenceDirectory 'xaml-resources.txt') -Encoding utf8
    $runtimeConfig = Get-Content (Join-Path $publishDirectory 'FireAxe.runtimeconfig.json') -Raw | ConvertFrom-Json -AsHashtable
    if ($runtimeConfig.runtimeOptions.ContainsKey('framework') -or $runtimeConfig.runtimeOptions.ContainsKey('frameworks')) {
        throw 'Publish still references an installed .NET shared framework.'
    }
    foreach ($packagePrefix in @('Microsoft.WindowsAppSDK.Runtime/', 'Microsoft.WindowsAppSDK.WinUI/', 'Microsoft.NETCore.App.Runtime.win-x64/')) {
        $matching = @($packageRecords | Where-Object { $_.package.StartsWith($packagePrefix, [StringComparison]::OrdinalIgnoreCase) })
        if ($matching.Count -eq 0 -or @($matching[0].files).Count -eq 0) { throw "Runtime license files are missing: $packagePrefix" }
    }

    $nativeDependencies = @(& (Join-Path $PSScriptRoot 'Test-PublishDependencies.ps1') -PublishDirectory $publishDirectory)
    $nativeDependencies | Export-Csv (Join-Path $evidenceDirectory 'native-dependencies.csv') -NoTypeInformation -Encoding utf8

    $zipPath = Join-Path $runDirectory "$artifactName.zip"
    [IO.Compression.ZipFile]::CreateFromDirectory($publishDirectory, $zipPath, [IO.Compression.CompressionLevel]::Optimal, $false)
    $archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $zipEntries = @($archive.Entries.FullName)
        foreach ($relative in $requiredFiles) {
            if ($relative -notin $zipEntries) { throw "Zip entry is missing: $relative" }
        }
        $publishedFiles = @(Get-ChildItem $publishDirectory -File -Recurse)
        $zipFileCount = @($archive.Entries | Where-Object { $_.Name.Length -gt 0 }).Count
        if ($zipFileCount -ne $publishedFiles.Count) { throw 'Zip does not contain all published files.' }
    }
    finally { $archive.Dispose() }
    $hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $checksumPath = "$zipPath.sha256"
    "$hash  $artifactName.zip" | Set-Content $checksumPath -Encoding ascii
    $manifestPath = Join-Path $runDirectory 'publish-manifest.json'
    $manifest = [ordered]@{
        source = 'scripts/Publish-Windows.ps1 (original P7 implementation)'
        version = $version
        runtimeIdentifier = 'win-x64'
        dotnetSelfContained = $true
        windowsAppSdkSelfContained = $true
        singleFile = $false
        testsExecuted = -not [bool] $SkipTests
        applicationLaunched = $false
        requiredFiles = $requiredFiles
        embeddedXamlResources = $xamlResources
        packageCount = $packageRecords.Count
        packages = @($packageRecords)
        fileCount = $publishedFiles.Count
        zipPath = $zipPath
        zipBytes = (Get-Item $zipPath).Length
        sha256 = $hash
        limitations = @('Static dependency checks do not replace a clean Windows VM launch.', 'Extract the complete zip before double-clicking FireAxe.exe.', 'WebView2 or operating system components may still be needed by SDK features; no runtime installer is run.')
    }
    $manifest | ConvertTo-Json -Depth 12 | Set-Content $manifestPath -Encoding utf8
    [pscustomobject]@{ Version = $version; ZipPath = $zipPath; ChecksumPath = $checksumPath; PublishDirectory = $publishDirectory; EvidenceDirectory = $evidenceDirectory; ManifestPath = $manifestPath }
}
finally { Pop-Location }
