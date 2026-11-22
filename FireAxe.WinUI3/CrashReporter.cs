// Ported from FireAxe_Alt\FireAxe.GUI\CrashReporter.cs
// The Linux branch is dropped: this project targets WinUI 3 (Windows only).
using System;
using System.Diagnostics;
using System.IO;
using FireAxe.Resources;
using Serilog;

namespace FireAxe;

/// <summary>
/// Shows a fatal error in a temporary console window so that a crash before the UI exists is readable.
/// </summary>
public static class CrashReporter
{
    private const string Title = "====FireAxeWinUI3 Crash Reporter====";

    public static void Run(Exception unhandledException)
    {
        ArgumentNullException.ThrowIfNull(unhandledException);

        string message = $"{Texts.CrashReporterMessageHeader}\n{unhandledException}";

        string? messagePath = null;
        string? batPath = null;

        try
        {
            messagePath = Path.GetTempFileName();
            batPath = Path.GetTempFileName() + ".bat";
            File.WriteAllText(messagePath, message);
            string bat = $"""
                @echo off
                chcp 65001
                @echo off
                echo.
                echo {Title}
                echo.
                type "{messagePath}"
                echo.
                pause
                """;
            File.WriteAllText(batPath, bat);
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = batPath,
                UseShellExecute = false,
            }) ?? throw new Exception("Failed to create a bat process.");
            process.WaitForExit();
        }
        catch (Exception ex)
        {
            LogException(ex);
        }

        try
        {
            if (messagePath is not null && File.Exists(messagePath))
            {
                File.Delete(messagePath);
            }
            if (batPath is not null && File.Exists(batPath))
            {
                File.Delete(batPath);
            }
        }
        catch (Exception ex)
        {
            LogException(ex);
        }

        void LogException(Exception ex) => Log.Error(ex, "Exception occurred during CrashReporter.Run.");
    }
}
