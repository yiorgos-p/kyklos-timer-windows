using System.Runtime.InteropServices;
using Kyklos.Engine;
using Microsoft.UI.Dispatching;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Win32;
using Windows.Win32.Media.Audio;

namespace Kyklos.Platform;

/// <summary>
/// Plays the alarm. The sounds that come with kyklos play through Windows' plain sound playback, which every edition of
/// Windows has; a sound of the user's own needs Windows' media playback, which Windows' N editions only have once the
/// Media Feature Pack is added. A media player is made only while it plays: holding one ready all the time costs memory
/// for something that rings a few times a day.
/// </summary>
public sealed class AlarmPlayer(DispatcherQueue queue) : IAlarmPlayer
{
    private MediaPlayer? _player;
    private MediaSource? _source;

    // Plain sound playback says nothing when a sound ends, so this goes off when it should have.
    private DispatcherQueueTimer? _builtInEnd;

    public event Action? Ended;

    public void Play(IReadOnlyList<string> files)
    {
        Stop();
        PlayFrom(files, 0);
    }

    public void Stop()
    {
        if (_builtInEnd is { } end)
        {
            _builtInEnd = null;
            end.Stop();
            PInvoke.PlaySound(null, null, 0);
        }

        if (_player is { } player)
        {
            _player = null;
            player.Pause();
            player.Dispose();
        }

        // The sound goes too: it holds its file open, and a custom sound can't be replaced while it's open.
        _source?.Dispose();
        _source = null;
    }

    public async Task<TimeSpan?> MeasureAsync(string file)
    {
        try
        {
            using var source = MediaSource.CreateFromUri(new Uri(file));
            await source.OpenAsync();
            return source.Duration;
        }
        catch (Exception)
        {
            // Whatever Media Foundation can't open counts as a file that can't be played.
            return null;
        }
    }

    /// <summary>Plays the file at this index, moving on to the next one if it doesn't open.</summary>
    private void PlayFrom(IReadOnlyList<string> files, int index)
    {
        if (index >= files.Count)
        {
            Ended?.Invoke();
            return;
        }

        if (AlarmSounds.IsBuiltInFile(files[index]))
        {
            if (!PlayBuiltIn(files[index]))
            {
                PlayFrom(files, index + 1);
            }

            return;
        }

        MediaPlayer player;
        try
        {
            _source = MediaSource.CreateFromUri(new Uri(files[index]));
            player = new MediaPlayer
            {
                AudioCategory = MediaPlayerAudioCategory.Alerts,
                Source = _source,
            };
        }
        catch (Exception e) when (e is COMException or UriFormatException)
        {
            // Windows' N editions come without media playback until the Media Feature Pack is added: the built-in sound
            // that's next in line plays instead.
            Stop();
            PlayFrom(files, index + 1);
            return;
        }

        // Keep the alarm out of the media controls in Windows' volume flyout.
        player.CommandManager.IsEnabled = false;

        // MediaPlayer reports from a background thread; deal with it on the UI thread, where Play and Stop run.
        player.MediaFailed += (sender, _) => queue.TryEnqueue(() =>
        {
            if (sender == _player)
            {
                Stop();
                PlayFrom(files, index + 1);
            }
        });
        player.MediaEnded += (sender, _) => queue.TryEnqueue(() =>
        {
            if (sender == _player)
            {
                Stop();
                Ended?.Invoke();
            }
        });

        _player = player;
        player.Play();
    }

    /// <summary>Plays one of kyklos's own sounds, or says it can't.</summary>
    private bool PlayBuiltIn(string file)
    {
        if (WaveFile.Length(file) is not { } length
            || !PInvoke.PlaySound(file, null, SND_FLAGS.SND_FILENAME | SND_FLAGS.SND_ASYNC | SND_FLAGS.SND_NODEFAULT))
        {
            return false;
        }

        var end = queue.CreateTimer();
        end.Interval = length;
        end.IsRepeating = false;
        end.Tick += (sender, _) =>
        {
            if (sender == _builtInEnd)
            {
                Stop();
                Ended?.Invoke();
            }
        };
        _builtInEnd = end;
        end.Start();
        return true;
    }
}
