# Note: this file preserves the local probe record for the P7 distribution attempt of
# 2026-11-14. It supersedes the "EXE read denied" reading of the 2026-11-11 records.

## What the 2026-11-11 records actually say

- `docs/evidence/P7/distribution-subagent-access-denied.txt` line 6: the failing path is
  `FireAxe.WinUI3\obj\x64\Release\net10.0-windows10.0.26100.0\win-x64\apphost.exe` (`MSB3021`).
  The error text is `Access to the path '…apphost.exe' is denied`, not the published `FireAxe.exe`.
- `docs/evidence/P7/final-validation.md` line 31-37 attributed the stop to a PE read of
  `publish\FireAxe.exe`. That path is readable today:

```powershell
Get-Item artifacts\p7-final\FireAxe-0.1.0-win-x64-20261111-140105-89984733\publish\FireAxe.exe
#    Length 303104, readable
```

## Local re-run, same command, 2026-11-14

```powershell
pwsh -File ./scripts/Publish-Windows.ps1 -ArtifactRoot artifacts/p7-ci-probe
# exit 0, Release 0 warnings / 0 errors, 18/18 tests, zip 99,625,003 bytes
pwsh -File ./scripts/Publish-Windows.ps1 -ArtifactRoot artifacts/p7-ci-probe2 -SkipTests
# exit 0, publish 540 files, zip 95 MB, sha256 + publish-manifest.json written
```

Both runs completed the PE dependency check (`Test-PublishDependencies.ps1`), the PRI
embedded-XAML dump, ZIP entry verification and SHA-256 generation. No `Access denied`
reproduced. Assessment: the 2026-11-11 stop was a transient file lock/permission condition in
that session (an apphost.exe held by a running or previously failed `dotnet publish`, with the
session's approval channel closed), not a property of the publish output.

## Consequence for the P7 record

- "发布 EXE 读取拒绝 🟥 分发验证阻断" is downgraded to 🟨 (observed once, not reproducible);
  see `docs/phase-notes/P7-release-readiness.md`.
- The remaining 🟥 item for distribution is unchanged: no zip has been launched on a clean
  Windows machine, and hosted CI still has to finish inside its timeout.
