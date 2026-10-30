namespace Kyklos.Engine;

/// <summary>
/// kyklos.exe carries .NET and the Windows App SDK inside it. The first time a version starts, .NET unpacks them into a
/// folder of that version's own in %TEMP%\.net\kyklos, and uses it from then on; it never removes the folders of
/// earlier versions, about 70 MB each, so kyklos deletes them once it's running.
/// </summary>
public static class UnpackedCopies
{
    /// <summary>A folder written to this recently may be one that another start of kyklos is unpacking right now.</summary>
    public static readonly TimeSpan Recent = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The folder the copies are unpacked into, %TEMP%\.net\kyklos (or the name kyklos.exe was saved under), or null
    /// when kyklos isn't running from an unpacked copy: a build that isn't a single exe runs where its exe is, and a
    /// copy anywhere unexpected is left alone.
    /// </summary>
    public static string? Home(string baseDirectory, string processPath)
    {
        var copy = Path.TrimEndingDirectorySeparator(baseDirectory);
        var home = Path.GetDirectoryName(copy);

        return home is not null
            && !SamePath(copy, Path.GetDirectoryName(processPath))
            && SamePath(Path.GetFileName(home), Path.GetFileNameWithoutExtension(processPath))
            && SamePath(Path.GetFileName(Path.GetDirectoryName(home)), ".net")
            ? home
            : null;
    }

    /// <summary>The copies to delete: all but the one running, and any written to just now.</summary>
    public static IEnumerable<string> Old(string baseDirectory, IEnumerable<(string Folder, DateTime LastWritten)> copies, DateTime now) =>
        copies
            .Where(copy => !SamePath(copy.Folder, Path.TrimEndingDirectorySeparator(baseDirectory)) && now - copy.LastWritten > Recent)
            .Select(copy => copy.Folder);

    /// <summary>Deletes earlier versions' copies, as far as it can; one with a file open in it goes another time.</summary>
    public static void DeleteOld(string baseDirectory, string processPath)
    {
        if (Home(baseDirectory, processPath) is not { } home)
        {
            return;
        }

        try
        {
            var copies = Directory.EnumerateDirectories(home).Select(folder => (folder, Directory.GetLastWriteTimeUtc(folder)));
            foreach (var folder in Old(baseDirectory, copies.ToList(), DateTime.UtcNow))
            {
                try
                {
                    Directory.Delete(folder, recursive: true);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // In use, or not deletable: the next start tries again.
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The folder went, or can't be read: nothing to tidy.
        }
    }

    private static bool SamePath(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
