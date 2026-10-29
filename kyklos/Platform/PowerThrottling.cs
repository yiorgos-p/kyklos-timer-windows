using Windows.Win32;
using Windows.Win32.System.Threading;

namespace Kyklos.Platform;

/// <summary>
/// Windows slows down apps it thinks nobody is watching, to save power ("efficiency mode"). While a session is on,
/// kyklos asks not to be, so the alarm comes on time even with its windows hidden; stopped or paused it does nothing at
/// all, and Windows may do as it likes.
/// </summary>
internal static class PowerThrottling
{
    public static unsafe void Allow(bool allowed)
    {
        var state = new PROCESS_POWER_THROTTLING_STATE
        {
            Version = PInvoke.PROCESS_POWER_THROTTLING_CURRENT_VERSION,

            // Execution speed taken in hand and left on full; or left to Windows.
            ControlMask = allowed ? 0 : PInvoke.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
            StateMask = 0,
        };
        PInvoke.SetProcessInformation(PInvoke.GetCurrentProcess(), PROCESS_INFORMATION_CLASS.ProcessPowerThrottling,
            &state, (uint)sizeof(PROCESS_POWER_THROTTLING_STATE));
    }
}
