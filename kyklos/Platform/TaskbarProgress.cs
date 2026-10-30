using System.Runtime.InteropServices;
using Kyklos.Engine;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace Kyklos.Platform;

/// <summary>
/// Progress on the main window's taskbar button, so the time can be seen with the window minimised: how much of the
/// session has gone, paused while paused, and full and red once time's up.
/// </summary>
internal sealed class TaskbarProgress(nint window)
{
    private ITaskbarList3? _taskbar;

    /// <summary>What the button shows, in whole percent: finer than the bar can draw, so most seconds change nothing.</summary>
    private (TBPFLAG State, ulong Percent)? _shown;

    /// <summary>Shows the timer's progress, or clears it for null (or while stopped).</summary>
    public void Show(TimerViewModel? timer)
    {
        var (state, percent) = timer is null || timer.State == TimerState.Stopped
            ? (TBPFLAG.TBPF_NOPROGRESS, 0UL)
            : (timer.State switch
            {
                TimerState.Paused => TBPFLAG.TBPF_PAUSED,
                TimerState.Finished => TBPFLAG.TBPF_ERROR,
                _ => TBPFLAG.TBPF_NORMAL,
            }, Gone(timer));

        if (_shown == (state, percent))
        {
            return;
        }

        try
        {
            var taskbar = _taskbar ??= Create();
            var button = new HWND(window);
            taskbar.SetProgressState(button, state);
            if (state != TBPFLAG.TBPF_NOPROGRESS)
            {
                taskbar.SetProgressValue(button, percent, 100);
            }

            _shown = (state, percent);
        }
        catch (COMException)
        {
            // Explorer may be restarting, with no taskbar to show it on: it's tried afresh the next time.
            _taskbar = null;
            _shown = null;
        }
    }

    /// <summary>Explorer has made the button anew (the window was shown again, or Explorer restarted), with nothing on it.</summary>
    public void Forget() => _shown = null;

    private static ulong Gone(TimerViewModel timer)
    {
        if (timer.State == TimerState.Finished)
        {
            return 100;
        }

        var total = Math.Max(1, timer.SessionLength);
        return (ulong)((total - Math.Min(timer.RemainingTime, total)) * 100L / total);
    }

    private static ITaskbarList3 Create()
    {
        var taskbar = (ITaskbarList3)new TaskbarList();
        taskbar.HrInit();
        return taskbar;
    }
}
