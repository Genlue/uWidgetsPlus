using System;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using Avalonia.Media.Imaging;

namespace Tools.Models;

public class ClipboardItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ClipboardType Type { get; set; }
    public string? Text { get; set; }
    public string[]? FilePaths { get; set; }
    public string? ImagePath { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;

    [JsonIgnore]
    public Bitmap? Thumbnail { get; set; }

    [JsonIgnore]
    public string RelativeTime
    {
        get
        {
            var span = DateTime.Now - Timestamp;
            if (span.TotalSeconds < 60) return "刚刚";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} 分钟前";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} 小时前";
            return Timestamp.ToString("MM-dd HH:mm");
        }
    }

    [JsonIgnore]
    public string DisplayTitle => Type switch
    {
        ClipboardType.Text => string.IsNullOrWhiteSpace(Text) ? "(空白文本)" : Text.Replace("\r", " ").Replace("\n", " ").Trim(),
        ClipboardType.Image => $"图片 ({ImageWidth}×{ImageHeight})",
        ClipboardType.Files => FilePaths?.Length > 0 ? Path.GetFileName(FilePaths[0]) : "文件",
        _ => "剪贴板条目"
    };

    [JsonIgnore]
    public string DisplaySubtitle => Type switch
    {
        ClipboardType.Text => $"{Text?.Length ?? 0} 字符",
        ClipboardType.Image => $"{ImageWidth}×{ImageHeight} 像素",
        ClipboardType.Files => FilePaths?.Length > 1 ? $"等共 {FilePaths.Length} 个文件" : (FilePaths?.Length == 1 ? FilePaths[0] : ""),
        _ => ""
    };
}
