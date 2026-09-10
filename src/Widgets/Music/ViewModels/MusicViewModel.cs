using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Music.Models;
using Music.Services;

namespace Music.ViewModels;

public class MusicViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly MediaManagerService mediaService;
    private readonly DispatcherTimer timer;
    private MusicModel model;

    private string trackTitle = "未在播放";
    private string artist = "启动音乐软件开始聆听";
    private string albumTitle = string.Empty;
    private string playerName = "音乐";
    private Bitmap? coverBitmap;
    private bool isPlaying;
    private bool hasTrack;
    private TimeSpan position = TimeSpan.Zero;
    private TimeSpan duration = TimeSpan.Zero;
    private double progressPercent;
    private string positionText = "00:00";
    private string durationText = "00:00";
    private bool canPlayPause = true;
    private bool canSkipNext = true;
    private bool canSkipPrevious = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MusicModel Model => model;

    public string TrackTitle
    {
        get => trackTitle;
        set => SetField(ref trackTitle, value);
    }

    public string Artist
    {
        get => artist;
        set => SetField(ref artist, value);
    }

    public string AlbumTitle
    {
        get => albumTitle;
        set => SetField(ref albumTitle, value);
    }

    public string PlayerName
    {
        get => playerName;
        set => SetField(ref playerName, value);
    }

    public Bitmap? CoverBitmap
    {
        get => coverBitmap;
        set => SetField(ref coverBitmap, value);
    }

    public bool IsPlaying
    {
        get => isPlaying;
        set => SetField(ref isPlaying, value);
    }

    public bool HasTrack
    {
        get => hasTrack;
        set => SetField(ref hasTrack, value);
    }

    public TimeSpan Position
    {
        get => position;
        set
        {
            if (SetField(ref position, value))
            {
                PositionText = FormatTime(value);
                UpdateProgress();
            }
        }
    }

    public TimeSpan Duration
    {
        get => duration;
        set
        {
            if (SetField(ref duration, value))
            {
                DurationText = FormatTime(value);
                UpdateProgress();
            }
        }
    }

    public double ProgressPercent
    {
        get => progressPercent;
        set => SetField(ref progressPercent, value);
    }

    public string PositionText
    {
        get => positionText;
        set => SetField(ref positionText, value);
    }

    public string DurationText
    {
        get => durationText;
        set => SetField(ref durationText, value);
    }

    public bool CanPlayPause
    {
        get => canPlayPause;
        set => SetField(ref canPlayPause, value);
    }

    public bool CanSkipNext
    {
        get => canSkipNext;
        set => SetField(ref canSkipNext, value);
    }

    public bool CanSkipPrevious
    {
        get => canSkipPrevious;
        set => SetField(ref canSkipPrevious, value);
    }

    public bool AmbientGlow => model.AmbientGlow;
    public bool ShowProgressBar => model.ShowProgressBar;

    public MusicViewModel(MusicModel model)
    {
        this.model = model;
        mediaService = new MediaManagerService();
        mediaService.UpdateRules(model.PlayerRules);

        mediaService.TrackChanged += OnTrackChanged;
        mediaService.TimelineChanged += OnTimelineChanged;

        timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        timer.Tick += OnTimerTick;
        timer.Start();
    }

    public void UpdateModel(MusicModel newModel)
    {
        model = newModel;
        mediaService.UpdateRules(model.PlayerRules);
        OnPropertyChanged(nameof(AmbientGlow));
        OnPropertyChanged(nameof(ShowProgressBar));
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (IsPlaying && Duration > TimeSpan.Zero && Position < Duration)
        {
            Position += TimeSpan.FromSeconds(1);
        }
    }

    private void OnTrackChanged(MediaTrackInfo track)
    {
        Dispatcher.UIThread.Post(() =>
        {
            HasTrack = track.HasTrack;
            TrackTitle = track.HasTrack ? track.Title : "未在播放";
            Artist = track.HasTrack ? (string.IsNullOrWhiteSpace(track.Artist) ? "未知艺术家" : track.Artist) : "启动音乐软件开始聆听";
            AlbumTitle = track.AlbumTitle;
            PlayerName = string.IsNullOrWhiteSpace(track.PlayerName) ? "音乐" : track.PlayerName;
            IsPlaying = track.IsPlaying;
            CanPlayPause = track.CanPlayPause;
            CanSkipNext = track.CanSkipNext;
            CanSkipPrevious = track.CanSkipPrevious;

            Position = track.Position;
            Duration = track.Duration;

            if (track.ThumbnailData != null && track.ThumbnailData.Length > 0)
            {
                try
                {
                    using var ms = new MemoryStream(track.ThumbnailData);
                    CoverBitmap = new Bitmap(ms);
                }
                catch
                {
                    CoverBitmap = null;
                }
            }
            else
            {
                CoverBitmap = null;
            }
        });
    }

    private void OnTimelineChanged(TimeSpan pos, TimeSpan dur)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Position = pos;
            Duration = dur;
        });
    }

    private void UpdateProgress()
    {
        if (duration.TotalSeconds > 0)
        {
            ProgressPercent = Math.Clamp(position.TotalSeconds / duration.TotalSeconds, 0.0, 1.0);
        }
        else
        {
            ProgressPercent = 0.0;
        }
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time.TotalHours >= 1)
            return time.ToString(@"h\:mm\:ss");
        return time.ToString(@"m\:ss");
    }

    public void TogglePlayPause()
    {
        _ = mediaService.TogglePlayPauseAsync();
    }

    public void SkipNext()
    {
        _ = mediaService.SkipNextAsync();
    }

    public void SkipPrevious()
    {
        _ = mediaService.SkipPreviousAsync();
    }

    public void Seek(double percent)
    {
        if (duration.TotalSeconds > 0)
        {
            var targetSeconds = duration.TotalSeconds * Math.Clamp(percent, 0.0, 1.0);
            var targetTime = TimeSpan.FromSeconds(targetSeconds);
            Position = targetTime;
            _ = mediaService.SeekAsync(targetTime);
        }
    }

    public async Task<List<(string AppId, string Title, string Artist, bool IsPlaying)>> GetActiveSessionsAsync()
    {
        return await mediaService.GetActiveSessionsAsync();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        timer.Stop();
        mediaService.TrackChanged -= OnTrackChanged;
        mediaService.TimelineChanged -= OnTimelineChanged;
        mediaService.Dispose();
        CoverBitmap?.Dispose();
    }
}
