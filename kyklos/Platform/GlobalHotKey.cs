using System.ComponentModel;
using Kyklos.Engine;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Kyklos.Platform;

/// <summary>
/// Keeps the global shortcut registered with Windows as Settings says: on, off, or set aside while a new one is
/// recorded. Windows tells the system window when it's pressed, whichever app is in front.
/// </summary>
internal sealed class GlobalHotKey : IDisposable
{
    private const int Id = 1;

    private readonly SystemWindow _window;
    private readonly Settings _settings;
    private bool _registered;
    private bool _suspended;

    public GlobalHotKey(SystemWindow window, Settings settings)
    {
        _window = window;
        _settings = settings;
        _window.Received += OnMessage;
        _settings.PropertyChanged += OnSettingChanged;
        Apply();
    }

    public event Action? Pressed;

    /// <summary>False when Windows wouldn't take the shortcut (another app has it), which Settings then says.</summary>
    public bool Works { get; private set; } = true;

    /// <summary>While a new shortcut is recorded, the old one is let go, so pressing it doesn't start the timer.</summary>
    public bool Suspended
    {
        get => _suspended;
        set
        {
            _suspended = value;
            Apply();
        }
    }

    public void Dispose()
    {
        _window.Received -= OnMessage;
        _settings.PropertyChanged -= OnSettingChanged;
        Release();
    }

    private void OnSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Settings.GlobalShortcut) or nameof(Settings.GlobalShortcutEnabled))
        {
            Apply();
        }
    }

    private void Apply()
    {
        Release();
        Works = true;

        if (!_settings.GlobalShortcutEnabled || _suspended)
        {
            return;
        }

        // Held down, a key repeats; one press should toggle the timer once.
        var shortcut = _settings.GlobalShortcut;
        _registered = PInvoke.RegisterHotKey(_window.Handle, Id,
            (HOT_KEY_MODIFIERS)shortcut.Modifiers | HOT_KEY_MODIFIERS.MOD_NOREPEAT, (uint)shortcut.Key);
        Works = _registered;
    }

    private void Release()
    {
        if (_registered)
        {
            PInvoke.UnregisterHotKey(_window.Handle, Id);
            _registered = false;
        }
    }

    private void OnMessage(uint message, nuint wParam, nint lParam)
    {
        if (message == PInvoke.WM_HOTKEY && wParam == Id)
        {
            Pressed?.Invoke();
        }
    }
}
