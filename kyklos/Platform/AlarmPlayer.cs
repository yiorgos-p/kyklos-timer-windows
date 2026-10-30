using System.Runtime.InteropServices;
using Kyklos.Engine;
using Microsoft.UI.Dispatching;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Kyklos.Platform;

/// <summary>
/// Plays the alarm, keeping the sound loaded only while it plays. Holding a player ready all the time costs memory for
/// something that rings a few times a day; loading it on demand takes a few milliseconds.
/// </summary>
public sealed class AlarmPlayer(DispatcherQueue queue) : IAlarmPlayer
{
    private MediaPlayer? _player;
    private MediaSource? _source;

    public event Action? Ended;

    public void Play(IReadOnlyList<string> files)
    {
        Stop();
        PlayFrom(files, 0);
    }

    public void Stop()
    {
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
            // Windows' N editions come without media playback until the Media Feature Pack is added: no sound then, but
            // time's up still goes on.
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
}
