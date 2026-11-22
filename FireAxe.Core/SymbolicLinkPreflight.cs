// P7: use the .NET symbolic-link API to check the destination before replacing a push.
namespace FireAxe;

public static class SymbolicLinkPreflight
{
    /// <summary>Checks link creation in the actual destination without changing existing push files.</summary>
    public static void Verify(string directoryPath)
    {
        // Windows allows a dangling file link. No target file or game data needs to be created.
        string probePath = Path.Join(directoryPath, $"fireaxe_probe_{Guid.NewGuid():N}.tmp");
        bool created = false;
        try
        {
            File.CreateSymbolicLink(probePath, probePath + ".target");
            created = true;
        }
        finally
        {
            if (created) File.Delete(probePath);
        }
    }
}
