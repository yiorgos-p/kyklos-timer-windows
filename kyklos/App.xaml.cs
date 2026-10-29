using Kyklos.Engine;
using Kyklos.Platform;
using Kyklos.Views;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.Win32;

namespace Kyklos;

/// <summary>Starts kyklos and owns what lives as long as it does: the settings, the timer and its windows.</summary>
public partial class App : Application
{
    /// <summary>Taskbar grouping and notifications go by this ID, as they go by the bundle ID on the Mac.</summary>
    private const string AppUserModelId = "io.github.yiorgos-p.Kyklos";

    private Settings _settings = null!;
    private TimerViewModel _timer = null!;
    private Notifier _notifier = null!;
    private MainWindow? _mainWindow;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        PInvoke.SetCurrentProcessExplicitAppUserModelID(AppUserModelId);

        var queue = DispatcherQueue.GetForCurrentThread();
        _settings = new Settings(Settings.DefaultFolder);
        _notifier = new Notifier(() => queue.TryEnqueue(ShowMainWindow));
        _timer = new TimerViewModel(_settings, new SystemClock(), new DispatcherScheduler(queue), new AlarmPlayer(queue), _notifier);

        ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            _mainWindow = new MainWindow(_timer, _settings);
            _mainWindow.Closed += OnMainWindowClosed;
        }

        _mainWindow.Activate();
    }

    private void OnMainWindowClosed(object sender, WindowEventArgs args)
    {
        // Closing the window quits kyklos, so keep the last keys typed and let a notification launch it again.
        _mainWindow = null;
        _timer.SaveTask();
        _notifier.Unregister();
    }
}
