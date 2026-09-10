using System.Diagnostics;
using Windows.Media.Control;
using Windows.Storage.Streams;
using Music.Models;

namespace Music.Services;

public class MediaManagerService : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? sessionManager;
    private GlobalSystemMediaTransportControlsSession? currentSession;
    private string? currentSessionId;
    private List<PlayerRule> rules = [];
    private bool disposed;

    public event Action<MediaTrackInfo>? TrackChanged;
    public event Action<TimeSpan, TimeSpan>? TimelineChanged;

    public MediaTrackInfo CurrentTrack { get; private set; } = new();

    private Timer? pollTimer;

    public MediaManagerService()
    {
        InitializeAsync();
    }

    public void UpdateRules(IEnumerable<PlayerRule> newRules)
    {
        rules = newRules.Where(r => r.IsEnabled).OrderBy(r => r.Priority).ToList();
        _ = RefreshActiveSessionAsync();
    }

    private async void InitializeAsync()
    {
        try
        {
            sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (sessionManager != null)
            {
                sessionManager.SessionsChanged += OnSessionsChanged;
                sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;
                await RefreshActiveSessionAsync();

                // Poll every 1.5s as WinRT SMTC events often do not fire for Win32 apps like QQMusic
                pollTimer = new Timer(async _ =>
                {
                    if (disposed) return;
                    try
                    {
                        await RefreshActiveSessionAsync();
                    }
                    catch { }
                }, null, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(1500));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaManagerService] Init error: {ex.Message}");
        }
    }

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
    {
        _ = RefreshActiveSessionAsync();
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        _ = RefreshActiveSessionAsync();
    }

    public async Task RefreshActiveSessionAsync()
    {
        if (sessionManager == null || disposed) return;

        try
        {
            var sessions = sessionManager.GetSessions();
            GlobalSystemMediaTransportControlsSession? bestSession = null;
            PlayerRule? matchedRule = null;

            // 1. Try to find a playing session matching rules (by rule priority)
            foreach (var rule in rules)
            {
                var match = sessions.FirstOrDefault(s =>
                    MatchesRule(rule, s.SourceAppUserModelId) &&
                    s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);

                if (match != null)
                {
                    bestSession = match;
                    matchedRule = rule;
                    break;
                }
            }

            // 2. If no rule-matched session is playing, check if ANY session is currently playing
            if (bestSession == null)
            {
                bestSession = sessions.FirstOrDefault(s =>
                    s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);
            }

            // 3. If no session is playing, find a paused session matching rules (by priority)
            if (bestSession == null)
            {
                foreach (var rule in rules)
                {
                    var match = sessions.FirstOrDefault(s =>
                        MatchesRule(rule, s.SourceAppUserModelId) &&
                        s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused);

                    if (match != null)
                    {
                        bestSession = match;
                        matchedRule = rule;
                        break;
                    }
                }
            }

            // 4. Fallback: take default current session from Windows SMTC
            if (bestSession == null)
            {
                bestSession = sessionManager.GetCurrentSession();
            }

            if (bestSession != null && matchedRule == null)
            {
                matchedRule = rules.FirstOrDefault(r => MatchesRule(r, bestSession.SourceAppUserModelId));
            }

            // Bind to the selected session
            await BindToSessionAsync(bestSession, matchedRule);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaManagerService] Refresh error: {ex.Message}");
        }
    }

    private bool MatchesRule(PlayerRule rule, string appId)
    {
        if (string.IsNullOrWhiteSpace(appId)) return false;
        var patterns = rule.MatchPattern.Split('|', StringSplitOptions.RemoveEmptyEntries);
        return patterns.Any(p => appId.Contains(p.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private async Task BindToSessionAsync(GlobalSystemMediaTransportControlsSession? session, PlayerRule? rule)
    {
        if (session == null)
        {
            UnbindCurrentSession();
            CurrentTrack = new MediaTrackInfo();
            TrackChanged?.Invoke(CurrentTrack);
            return;
        }

        var appId = session.SourceAppUserModelId;
        if (currentSession != null && currentSessionId == appId)
        {
            // Same session, refresh its state
            await UpdateTrackPropertiesAsync(session, rule);
            return;
        }

        UnbindCurrentSession();

        currentSession = session;
        currentSessionId = appId;

        currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
        currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
        currentSession.TimelinePropertiesChanged += OnTimelinePropertiesChanged;

        await UpdateTrackPropertiesAsync(currentSession, rule);
    }

    private void UnbindCurrentSession()
    {
        if (currentSession != null)
        {
            currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            currentSession.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
            currentSession = null;
            currentSessionId = null;
        }
    }

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        _ = UpdateTrackPropertiesAsync(sender, null);
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        _ = UpdateTrackPropertiesAsync(sender, null);
    }

    private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
    {
        UpdateTimeline(sender);
    }

    private async Task UpdateTrackPropertiesAsync(GlobalSystemMediaTransportControlsSession session, PlayerRule? rule)
    {
        try
        {
            var playback = session.GetPlaybackInfo();
            var props = await session.TryGetMediaPropertiesAsync();
            var timeline = session.GetTimelineProperties();

            var isPlaying = playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var title = props?.Title ?? string.Empty;
            var artist = props?.Artist ?? string.Empty;
            var album = props?.AlbumTitle ?? string.Empty;
            var appId = session.SourceAppUserModelId;

            // If track identity and playback status are unchanged, just update timeline
            if (currentSessionId == appId &&
                CurrentTrack.Title == title &&
                CurrentTrack.Artist == artist &&
                CurrentTrack.AlbumTitle == album &&
                CurrentTrack.IsPlaying == isPlaying &&
                (CurrentTrack.ThumbnailData != null || props?.Thumbnail == null))
            {
                CurrentTrack.Position = timeline.Position;
                CurrentTrack.Duration = timeline.EndTime;
                TimelineChanged?.Invoke(timeline.Position, timeline.EndTime);
                return;
            }

            var track = new MediaTrackInfo
            {
                Title = title,
                Artist = artist,
                AlbumTitle = album,
                SourceAppId = appId,
                PlayerName = rule?.Name ?? ResolveAppFriendlyName(appId),
                IsPlaying = isPlaying,
                Position = timeline.Position,
                Duration = timeline.EndTime,
                CanPlayPause = playback?.Controls.IsPlayPauseToggleEnabled ?? true,
                CanSkipNext = playback?.Controls.IsNextEnabled ?? true,
                CanSkipPrevious = playback?.Controls.IsPreviousEnabled ?? true,
            };

            if (props?.Thumbnail != null)
            {
                try
                {
                    using var stream = await props.Thumbnail.OpenReadAsync();
                    using var netStream = stream.AsStreamForRead();
                    using var ms = new MemoryStream();
                    await netStream.CopyToAsync(ms);
                    track.ThumbnailData = ms.ToArray();
                }
                catch
                {
                    track.ThumbnailData = null;
                }
            }

            CurrentTrack = track;
            TrackChanged?.Invoke(track);
            TimelineChanged?.Invoke(track.Position, track.Duration);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaManagerService] UpdateTrackProperties error: {ex.Message}");
        }
    }

    private void UpdateTimeline(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            var timeline = session.GetTimelineProperties();
            CurrentTrack.Position = timeline.Position;
            CurrentTrack.Duration = timeline.EndTime;
            TimelineChanged?.Invoke(timeline.Position, timeline.EndTime);
        }
        catch { }
    }

    public async Task<List<(string AppId, string Title, string Artist, bool IsPlaying)>> GetActiveSessionsAsync()
    {
        var result = new List<(string, string, string, bool)>();
        if (sessionManager == null) return result;

        try
        {
            foreach (var s in sessionManager.GetSessions())
            {
                var playback = s.GetPlaybackInfo();
                var props = await s.TryGetMediaPropertiesAsync();
                result.Add((
                    s.SourceAppUserModelId,
                    props?.Title ?? "无标题",
                    props?.Artist ?? "未知艺术家",
                    playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
                ));
            }
        }
        catch { }

        return result;
    }

    public async Task TogglePlayPauseAsync()
    {
        if (currentSession == null) return;
        try
        {
            var info = currentSession.GetPlaybackInfo();
            if (info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            {
                await currentSession.TryPauseAsync();
            }
            else
            {
                await currentSession.TryPlayAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaManagerService] PlayPause error: {ex.Message}");
        }
    }

    public async Task SkipNextAsync()
    {
        if (currentSession == null) return;
        try
        {
            await currentSession.TrySkipNextAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaManagerService] SkipNext error: {ex.Message}");
        }
    }

    public async Task SkipPreviousAsync()
    {
        if (currentSession == null) return;
        try
        {
            await currentSession.TrySkipPreviousAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaManagerService] SkipPrevious error: {ex.Message}");
        }
    }

    public async Task SeekAsync(TimeSpan position)
    {
        if (currentSession == null) return;
        try
        {
            await currentSession.TryChangePlaybackPositionAsync(position.Ticks);
            CurrentTrack.Position = position;
            TimelineChanged?.Invoke(position, CurrentTrack.Duration);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaManagerService] Seek error: {ex.Message}");
        }
    }

    private string ResolveAppFriendlyName(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId)) return "音乐";
        if (appId.Contains("cloudmusic", StringComparison.OrdinalIgnoreCase)) return "网易云音乐";
        if (appId.Contains("qqmusic", StringComparison.OrdinalIgnoreCase)) return "QQ音乐";
        if (appId.Contains("spotify", StringComparison.OrdinalIgnoreCase)) return "Spotify";
        if (appId.Contains("apple", StringComparison.OrdinalIgnoreCase)) return "Apple Music";
        if (appId.Contains("kugou", StringComparison.OrdinalIgnoreCase)) return "酷狗音乐";
        if (appId.Contains("chrome", StringComparison.OrdinalIgnoreCase)) return "Chrome";
        if (appId.Contains("msedge", StringComparison.OrdinalIgnoreCase)) return "Edge";
        if (appId.Contains("firefox", StringComparison.OrdinalIgnoreCase)) return "Firefox";
        return Path.GetFileNameWithoutExtension(appId);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        pollTimer?.Dispose();
        pollTimer = null;
        UnbindCurrentSession();
        if (sessionManager != null)
        {
            sessionManager.SessionsChanged -= OnSessionsChanged;
            sessionManager.CurrentSessionChanged -= OnCurrentSessionChanged;
            sessionManager = null;
        }
    }
}
