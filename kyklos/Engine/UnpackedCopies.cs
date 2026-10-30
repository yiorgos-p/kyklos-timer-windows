using Microsoft.Win32.SafeHandles;

namespace Kyklos.Engine;

/// <summary>
/// kyklos.exe carries .NET and the Windows App SDK inside it. The first time a version starts, .NET unpacks them into a
/// folder of that version's own in %TEMP%\.net\kyklos, and uses it from then on; it never removes the folders of
/// earlier versions, about 70 MB each, so kyklos deletes them once it's running. It also keeps its own copy whole while
/// it runs.
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

    /// <summary>
    /// Deletes earlier versions' copies. One that's in use (an earlier kyklos still running, in another sign-in session
    /// say) is left for another time, and a folder that isn't a copy of kyklos at all (another app saved under the same
    /// name unpacks into the same place) is never touched.
    /// </summary>
    public static void DeleteOld(string baseDirectory, string processPath)
    {
        if (Home(baseDirectory, processPath) is not { } home)
        {
            return;
        }

        try
        {
            var copies = Directory.EnumerateDirectories(home).Select(folder => (folder, Directory.GetLastWriteTimeUtc(folder)));
            foreach (var folder in Old(baseDirectory, copies.ToList(), DateTime.UtcNow).Where(Unused))
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

    /// <summary>
    /// Keeps the running copy whole until kyklos ends: every file in it stays open, for others to read but not to
    /// delete. Windows' clean-ups of old temporary files would otherwise delete the files kyklos only opens when it needs
    /// them, like the alarm sounds, from under a copy that has been running for days; .NET puts back what's missing only
    /// when kyklos next starts.
    /// </summary>
    public static IReadOnlyList<SafeFileHandle> Keep(string baseDirectory, string processPath)
    {
        var kept = new List<SafeFileHandle>();
        if (Home(baseDirectory, processPath) is null)
        {
            return kept;
        }

        foreach (var file in Directory.EnumerateFiles(baseDirectory, "*", SearchOption.AllDirectories))
        {
            try
            {
                kept.Add(File.OpenHandle(file, FileMode.Open, FileAccess.Read, FileShare.Read));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Already gone, or open in a way that won't share: there's nothing more to do for that one.
            }
        }

        return kept;
    }

    /// <summary>
    /// Whether a folder is a copy of kyklos that nothing runs from: its kyklos.dll (named so whatever the exe is called)
    /// can be opened by this process alone. A running copy has it loaded, and keeps it open.
    /// </summary>
    private static bool Unused(string folder)
    {
        try
        {
            File.OpenHandle(Path.Combine(folder, "kyklos.dll"), FileMode.Open, FileAccess.Read, FileShare.None).Dispose();
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool SamePath(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
