// Source: original P7 isolated-profile paths for portable installs and release validation.
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FireAxe;

public static class StartupPaths
{
    public const string DataDirectoryVariable = "FIREAXE_DATA_DIRECTORY";
    public static string DataDirectory { get; } = ResolveDataDirectory(Environment.GetEnvironmentVariable(DataDirectoryVariable));
    public static string LogPath => Path.Join(DataDirectory, "Logs", "Log.txt");
    public static string InstanceKey => "cecf873b6deb4f2790bbddd75964bbca-" +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(DataDirectory.ToUpperInvariant())));

    public static string ResolveDataDirectory(string? overridePath)
    {
        if (string.IsNullOrWhiteSpace(overridePath))
            return Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FireAxe");
        if (!Path.IsPathFullyQualified(overridePath))
            throw new ArgumentException($"{DataDirectoryVariable} must be an absolute path.", nameof(overridePath));
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(overridePath));
    }
}
