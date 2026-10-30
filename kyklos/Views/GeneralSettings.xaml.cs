using System.ComponentModel;
using System.Security;
using Kyklos.Engine;
using Kyklos.Platform;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Core;

namespace Kyklos.Views;

/// <summary>Settings → General: the alarm and the controls, how kyklos shows itself, and how it fits into Windows.</summary>
public sealed partial class GeneralSettings : UserControl
{
    /// <summary>The sound list's last item, which opens a file picker rather than choosing a sound.</summary>
    private const string ChooseFile = "choose";

    private static readonly string[] AudioTypes = [".mp3", ".m4a", ".aac", ".wav", ".wma", ".flac"];

    private readonly TimerViewModel _timer;
    private readonly Settings _settings;
    private readonly GlobalHotKey _hotKey;

    /// <summary>While the controls are being set from the settings, so they don't write them straight back.</summary>
    private bool _updating;

    /// <summary>The sound was played from here, so leaving Settings stops it (a ringing alarm rings on).</summary>
    private bool _previewing;

    // Whether the sound list is open. Closed, the arrow keys step through the sounds, and stepping onto "Choose file…"
    // mustn't open a picker: it does when it's picked from the open list.
    private bool _listOpen;

    private bool _recording;

    internal GeneralSettings(TimerViewModel timer, Settings settings, GlobalHotKey hotKey)
    {
        _timer = timer;
        _settings = settings;
        _hotKey = hotKey;
        InitializeComponent();

        Loaded += (_, _) =>
        {
            _settings.PropertyChanged += OnSettingChanged;
            _timer.PropertyChanged += OnTimerChanged;
            FillAlarmSounds();
            Refresh();
            RefreshNotifications();

            // Windows may not have taken the shortcut when kyklos started.
            ShowShortcutMessage(_hotKey.Works ? null : GlobalShortcut.TakenMessage);
        };
        Unloaded += (_, _) =>
        {
            _settings.PropertyChanged -= OnSettingChanged;
            _timer.PropertyChanged -= OnTimerChanged;
            StopPreview();
            StopRecording();
        };
    }

    /// <summary>
    /// Shown when notifications are turned off for kyklos or for everything, which Windows Settings can undo. (Run as
    /// administrator, kyklos can't post notifications at all, and Settings can't change that.) Notifications can be
    /// turned on or off meanwhile, so the window checks again when it's activated.
    /// </summary>
    public void RefreshNotifications()
    {
        var off = AppNotificationManager.Default.Setting
            is AppNotificationSetting.DisabledForApplication or AppNotificationSetting.DisabledForUser;

        // Closed, an InfoBar still keeps its margin; collapsed, it leaves no gap.
        NotificationsOff.IsOpen = off;
        NotificationsOff.Visibility = off ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        // A new custom sound changes the list; any other choice of sound only what's selected in it.
        if (e.PropertyName == nameof(Settings.CustomAlarmName))
        {
            FillAlarmSounds();
        }

        Refresh();
    }

    private void Refresh()
    {
        _updating = true;
        RepeatAlarm.IsOn = _settings.RepeatAlarm;
        SelectCurrentSound();
        ControlsChoice.SelectedIndex = _settings.UseMouseControls ? 1 : 0;
        ZenMode.IsOn = _settings.ZenMode;
        MinimalModeChoice.SelectedIndex = (int)_settings.MinimalMode;
        ThemeChoice.SelectedIndex = (int)_settings.Appearance;
        ShowInTaskbar.IsOn = _settings.ShowInTaskbar;
        StartWithWindowsSwitch.IsOn = StartWithWindows.IsOn;
        ShortcutEnabled.IsOn = _settings.GlobalShortcutEnabled;
        ShortcutButton.IsEnabled = _settings.GlobalShortcutEnabled;
        ShowShortcut(_recording ? "Type shortcut…" : _settings.GlobalShortcut.Describe(KeyNames.OnLayout));
        PlayButton.Content = _timer.IsAlarmPlaying ? "Stop" : "Play";
        _updating = false;
    }

    private void OnTimerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TimerViewModel.IsAlarmPlaying))
        {
            return;
        }

        // Back to Play when the sound ends by itself.
        _previewing &= _timer.IsAlarmPlaying;
        PlayButton.Content = _timer.IsAlarmPlaying ? "Stop" : "Play";
    }

    private async void OnOpenNotificationSettings(object sender, RoutedEventArgs e) =>
        await Launcher.LaunchUriAsync(new Uri("ms-settings:notifications"));

    private void OnRepeatAlarmToggled(object sender, RoutedEventArgs e) => Write(() => _settings.RepeatAlarm = RepeatAlarm.IsOn);

    private void OnControlsSelected(object sender, SelectionChangedEventArgs e) =>
        Write(() => _settings.UseMouseControls = ControlsChoice.SelectedIndex == 1);

    private void OnZenModeToggled(object sender, RoutedEventArgs e) => Write(() => _settings.ZenMode = ZenMode.IsOn);

    private void OnMinimalModeSelected(object sender, SelectionChangedEventArgs e) =>
        Write(() => _settings.MinimalMode = (MinimalMode)MinimalModeChoice.SelectedIndex);

    private void OnThemeSelected(object sender, SelectionChangedEventArgs e) =>
        Write(() => _settings.Appearance = (AppTheme)ThemeChoice.SelectedIndex);

    private void OnShowInTaskbarToggled(object sender, RoutedEventArgs e) => Write(() => _settings.ShowInTaskbar = ShowInTaskbar.IsOn);

    private void OnStartWithWindowsToggled(object sender, RoutedEventArgs e) => Write(() =>
    {
        try
        {
            StartWithWindows.IsOn = StartWithWindowsSwitch.IsOn;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException or IOException)
        {
            // Windows wouldn't change it; show how it really is.
            Refresh();
        }
    });

    /// <summary>A change made here, rather than the control following the settings.</summary>
    private void Write(Action change)
    {
        if (!_updating)
        {
            change();
        }
    }

    // Alarm sound

    private void FillAlarmSounds()
    {
        _updating = true;
        AlarmSound.Items.Clear();

        foreach (var name in AlarmSounds.BuiltIn)
        {
            AlarmSound.Items.Add(new ComboBoxItem { Content = name, Tag = name });
        }

        // A line between kyklos's own sounds and the user's, which can't be chosen.
        var separator = new ComboBoxItem
        {
            Content = new MenuFlyoutSeparator { IsTabStop = false },
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            IsEnabled = false,
            IsTabStop = false,
        };
        AutomationProperties.SetAccessibilityView(separator, AccessibilityView.Raw);
        AlarmSound.Items.Add(separator);

        if (_settings.CustomAlarmName is { } custom)
        {
            AlarmSound.Items.Add(new ComboBoxItem { Content = custom, Tag = AlarmSounds.Custom });
        }

        AlarmSound.Items.Add(new ComboBoxItem { Content = "Choose file…", Tag = ChooseFile });
        SelectCurrentSound();
        _updating = false;
    }

    /// <summary>The sound in use. One that isn't there any more (a removed custom file, say) reads as the default.</summary>
    private void SelectCurrentSound()
    {
        var current = _settings.AlarmSound;
        var valid = AlarmSounds.BuiltIn.Contains(current) || (current == AlarmSounds.Custom && _settings.CustomAlarmName is not null);
        var tag = valid ? current : AlarmSounds.Default;
        AlarmSound.SelectedItem = AlarmSound.Items.OfType<ComboBoxItem>().FirstOrDefault(item => item.Tag as string == tag);
    }

    private void OnAlarmSoundSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || AlarmSound.SelectedItem is not ComboBoxItem { Tag: string choice })
        {
            return;
        }

        StopPreview();
        ShowAlarmMessage(null);

        if (choice == ChooseFile)
        {
            // The sound in use stays chosen while the picker is open, which waits for the list to close. Stepped onto
            // with the list closed, it's passed over.
            _updating = true;
            SelectCurrentSound();
            _updating = false;
            if (_listOpen)
            {
                DispatcherQueue.TryEnqueue(ChooseSound);
            }

            return;
        }

        // Picking a sound plays it, as Windows' own sound settings do.
        _settings.AlarmSound = choice;
        StartPreview();
    }

    private void OnAlarmListOpened(object sender, object e) => _listOpen = true;

    // Only once the list has made its choice, which can come just after it closes.
    private void OnAlarmListClosed(object sender, object e) => DispatcherQueue.TryEnqueue(() => _listOpen = false);

    private async void ChooseSound()
    {
        // Settings may have closed, and let this page go, since the list asked for the picker.
        if (XamlRoot is not { } root)
        {
            return;
        }

        // The Windows App SDK's picker, not the older Windows.Storage one, which fails in an app run as administrator.
        var picker = new FileOpenPicker(root.ContentIslandEnvironment.AppWindowId)
        {
            SuggestedStartLocation = PickerLocationId.MusicLibrary,
            CommitButtonText = "Use sound",
        };
        foreach (var type in AudioTypes)
        {
            picker.FileTypeFilter.Add(type);
        }

        PickFileResult? file;
        try
        {
            file = await picker.PickSingleFileAsync();
        }
        catch (Exception)
        {
            // Whatever Windows' error, it comes as one exception or another; the timer carries on all the same.
            ShowAlarmMessage(AlarmSounds.NoPickerMessage);
            return;
        }

        if (file is null)
        {
            return;
        }

        var message = await _timer.SetCustomAlarmAsync(file.Path);
        ShowAlarmMessage(message);

        if (message is null)
        {
            StartPreview();
        }
    }

    private void OnPlayClicked(object sender, RoutedEventArgs e)
    {
        if (_timer.IsAlarmPlaying)
        {
            _previewing = false;
            _timer.StopAlarmSound();
        }
        else
        {
            StartPreview();
        }
    }

    private void StartPreview()
    {
        _timer.PlayAlarmSound();
        _previewing = true;
    }

    private void StopPreview()
    {
        if (_previewing)
        {
            _previewing = false;
            _timer.StopAlarmSound();
        }
    }

    private void ShowAlarmMessage(string? message) => ShowMessage(AlarmMessage, message);

    // Global shortcut

    private void OnShortcutEnabledToggled(object sender, RoutedEventArgs e) => Write(() =>
    {
        StopRecording();
        _settings.GlobalShortcutEnabled = ShortcutEnabled.IsOn;
        ShowShortcutMessage(_hotKey.Works ? null : GlobalShortcut.TakenMessage);
    });

    private void OnShortcutClicked(object sender, RoutedEventArgs e)
    {
        if (_recording)
        {
            StopRecording();
            return;
        }

        // The current shortcut is let go meanwhile, so pressing it doesn't start the timer.
        ShowShortcutMessage(null);
        _recording = true;
        _hotKey.Suspended = true;
        ShowShortcut("Type shortcut…");
    }

    private void OnShortcutLostFocus(object sender, RoutedEventArgs e) => StopRecording();

    private void StopRecording()
    {
        if (!_recording)
        {
            return;
        }

        // Registers the shortcut again, new or old, so Works says whether Windows took it.
        _recording = false;
        _hotKey.Suspended = false;
        ShowShortcut(_settings.GlobalShortcut.Describe(KeyNames.OnLayout));
    }

    // Read out as what it is, not only as the keys.
    private void ShowShortcut(string text)
    {
        ShortcutButton.Content = text;
        AutomationProperties.SetName(ShortcutButton, "Shortcut: " + text);
    }

    private void OnShortcutKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_recording)
        {
            return;
        }

        e.Handled = true;

        if (e.Key == VirtualKey.Escape)
        {
            StopRecording();
            return;
        }

        // The modifiers come first; wait for the key that goes with them.
        if (IsModifier(e.Key))
        {
            return;
        }

        var shortcut = new GlobalShortcut((int)e.Key, HeldModifiers());
        if (!shortcut.IsValid)
        {
            ShowShortcutMessage(GlobalShortcut.InvalidMessage);
            return;
        }

        _settings.GlobalShortcut = shortcut;
        StopRecording();
        ShowShortcutMessage(_hotKey.Works ? null : GlobalShortcut.TakenMessage);
    }

    private void OnShortcutKeyUp(object sender, KeyRoutedEventArgs e) => e.Handled = _recording;

    private void ShowShortcutMessage(string? message) => ShowMessage(ShortcutMessage, message);

    private static bool IsModifier(VirtualKey key) => key is VirtualKey.Control or VirtualKey.LeftControl
        or VirtualKey.RightControl or VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu or VirtualKey.Shift
        or VirtualKey.LeftShift or VirtualKey.RightShift or VirtualKey.LeftWindows or VirtualKey.RightWindows;

    private static ShortcutModifiers HeldModifiers()
    {
        var modifiers = ShortcutModifiers.None;

        if (IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows))
        {
            modifiers |= ShortcutModifiers.Windows;
        }

        if (IsDown(VirtualKey.Control))
        {
            modifiers |= ShortcutModifiers.Control;
        }

        if (IsDown(VirtualKey.Menu))
        {
            modifiers |= ShortcutModifiers.Alt;
        }

        if (IsDown(VirtualKey.Shift))
        {
            modifiers |= ShortcutModifiers.Shift;
        }

        return modifiers;
    }

    private static bool IsDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);

    /// <summary>An orange line under a setting, saying what went wrong.</summary>
    private static void ShowMessage(TextBlock line, string? message)
    {
        line.Text = message ?? "";
        line.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;

        // Screen readers say it as it appears: it's what went wrong with what was just done.
        if (message is not null)
        {
            FrameworkElementAutomationPeer.CreatePeerForElement(line).RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
    }
}
