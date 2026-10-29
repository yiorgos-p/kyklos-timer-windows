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

    /// <summary>Shows the timer's progress, or clears it for null (or while stopped).</summary>
    public void Show(TimerViewModel? timer)
    {
        var taskbar = _taskbar ??= Create();
        var button = new HWND(window);

        if (timer is null || timer.State == TimerState.Stopped)
        {
            taskbar.SetProgressState(button, TBPFLAG.TBPF_NOPROGRESS);
            return;
        }

        var total = (ulong)Math.Max(1, timer.SessionLength);
        var gone = timer.State == TimerState.Finished ? total : total - (ulong)Math.Min(timer.RemainingTime, (int)total);

        taskbar.SetProgressState(button, timer.State switch
        {
            TimerState.Paused => TBPFLAG.TBPF_PAUSED,
            TimerState.Finished => TBPFLAG.TBPF_ERROR,
            _ => TBPFLAG.TBPF_NORMAL,
        });
        taskbar.SetProgressValue(button, gone, total);
    }

    private static ITaskbarList3 Create()
    {
        var taskbar = (ITaskbarList3)new TaskbarList();
        taskbar.HrInit();
        return taskbar;
    }
}
