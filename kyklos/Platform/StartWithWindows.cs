using Microsoft.Win32;

namespace Kyklos.Platform;

/// <summary>
/// "Start with Windows": an entry in the user's Run key that starts this kyklos.exe at sign-in. (Packaged for the Store,
/// this would be a StartupTask instead.)
/// </summary>
internal static class StartWithWindows
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// Where Windows' own list of startup apps, in Settings and Task Manager, records an entry it has turned off; the
    /// entry itself stays in Run.
    /// </summary>
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    private const string ValueName = "kyklos";

    private static string Command => $"\"{Environment.ProcessPath}\"";

    /// <summary>
    /// On only when the entry starts this copy, so a kyklos that was moved shows it off; turning it on again puts the new
    /// place in, as on the Mac. Turned off in Windows' startup apps, it's off here too, as the Mac's follows Login Items,
    /// and turning it on here turns it on there.
    /// </summary>
    public static bool IsOn
    {
        get
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey);
            if (!string.Equals(run?.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // The record's first byte is odd while Windows has the entry turned off.
            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey);
            return approved?.GetValue(ValueName) is not byte[] { Length: > 0 } record || (record[0] & 1) == 0;
        }
        set
        {
            using (var run = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (value)
                {
                    run.SetValue(ValueName, Command);
                }
                else
                {
                    run.DeleteValue(ValueName, throwOnMissingValue: false);
                }
            }

            // With no record, Windows counts the entry as on.
            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
            approved?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
