using Music.Models;

namespace Music.Services;

public static class PresetPlayers
{
    public static List<PlayerRule> CreateDefaultRules() =>
    [
        new("网易云音乐", "cloudmusic", true, 0),
        new("QQ音乐", "qqmusic", true, 1),
        new("Spotify", "spotify", true, 2),
        new("Apple Music", "applemusic", true, 3),
        new("汽水音乐", "qishui", true, 4),
        new("酷狗音乐", "kugou", true, 5),
        new("酷我音乐", "kwmusic", true, 6),
        new("Foobar2000", "foobar2000", true, 7),
        new("YesPlayMusic", "yesplaymusic", true, 8),
        new("Windows 媒体播放器", "mediaplayer", true, 9),
        new("浏览器 (Edge/Chrome/Firefox)", "chrome|msedge|firefox|brave|43f3c7f1c0e34091", true, 10),
    ];
}
