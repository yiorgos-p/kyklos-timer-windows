using Microsoft.Win32;

namespace Kyklos.Platform;

/// <summary>
/// "Start with Windows": an entry in the user's Run key that starts this kyklos.exe at sign-in. (Packaged for the Store,
/// this would be a StartupTask instead.)
/// </summary>
internal static class StartWithWindows
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "kyklos";

    private static string Command => $"\"{Environment.ProcessPath}\"";

    /// <summary>
    /// On only when the entry starts this copy, so a kyklos that was moved shows it off; turning it on again puts the new
    /// place in, as on the Mac.
    /// </summary>
    public static bool IsOn
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return string.Equals(key?.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase);
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value)
            {
                key.SetValue(ValueName, Command);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
    }
}
