using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SkiaSharp;
using Tools.Models;

namespace Tools.Services;

public class ClipboardMonitorService
{
    private static ClipboardMonitorService? instance;
    public static ClipboardMonitorService Instance => instance ??= new ClipboardMonitorService();

    private readonly DispatcherTimer timer;
    private uint lastSequence;
    private uint lastSelfSequence;
    private readonly string storageDir;
    private readonly string cacheDir;
    private readonly string historyFilePath;

    public ObservableCollection<ClipboardItem> History { get; } = [];
    public event Action? HistoryChanged;

    private ClipboardModel currentModel = new();

    public ClipboardMonitorService()
    {
        storageDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "uWidgets");
        cacheDir = Path.Combine(storageDir, "ClipboardCache");
        historyFilePath = Path.Combine(storageDir, "clipboard_history.json");

        try
        {
            if (!Directory.Exists(cacheDir))
            {
                Directory.CreateDirectory(cacheDir);
            }
        }
        catch { }

        LoadHistory();
        lastSequence = ClipboardNative.GetClipboardSequenceNumber();

        timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        timer.Tick += OnTimerTick;
        timer.Start();
    }

    public void UpdateSettings(ClipboardModel model)
    {
        currentModel = model;
        TrimHistory(model.MaxHistoryCount);
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        uint currentSeq = ClipboardNative.GetClipboardSequenceNumber();
        if (currentSeq == lastSequence) return;

        lastSequence = currentSeq;
        if (currentSeq == lastSelfSequence) return;

        CheckAndCaptureClipboard();
    }

    private void CheckAndCaptureClipboard()
    {
        try
        {
            // 1. Files
            if (currentModel.CaptureFiles && ClipboardNative.IsClipboardFormatAvailable(ClipboardNative.CF_HDROP))
            {
                var files = ClipboardNative.ReadFiles();
                if (files != null && files.Length > 0)
                {
                    // Check if identical to current top item
                    var top = History.FirstOrDefault();
                    if (top?.Type == ClipboardType.Files && top.FilePaths != null && top.FilePaths.SequenceEqual(files))
                    {
                        return;
                    }

                    var item = new ClipboardItem
                    {
                        Type = ClipboardType.Files,
                        FilePaths = files,
                        Timestamp = DateTime.Now
                    };
                    AddItem(item);
                    return;
                }
            }

            // 2. Images
            if (currentModel.CaptureImages && (ClipboardNative.IsClipboardFormatAvailable(ClipboardNative.CF_DIB) || ClipboardNative.IsClipboardFormatAvailable(ClipboardNative.CF_DIBV5)))
            {
                using var skBitmap = ClipboardNative.ReadImage();
                if (skBitmap != null && skBitmap.Width > 0 && skBitmap.Height > 0)
                {
                    string fileName = $"{Guid.NewGuid():N}.png";
                    string filePath = Path.Combine(cacheDir, fileName);

                    using (var image = SKImage.FromBitmap(skBitmap))
                    using (var data = image.Encode(SKEncodedImageFormat.Png, 95))
                    using (var stream = File.OpenWrite(filePath))
                    {
                        data.SaveTo(stream);
                    }

                    var item = new ClipboardItem
                    {
                        Type = ClipboardType.Image,
                        ImagePath = filePath,
                        ImageWidth = skBitmap.Width,
                        ImageHeight = skBitmap.Height,
                        Timestamp = DateTime.Now
                    };

                    try
                    {
                        item.Thumbnail = new Bitmap(filePath);
                    }
                    catch { }

                    AddItem(item);
                    return;
                }
            }

            // 3. Text
            if (currentModel.CaptureText && ClipboardNative.IsClipboardFormatAvailable(ClipboardNative.CF_UNICODETEXT))
            {
                var text = ClipboardNative.ReadText();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var top = History.FirstOrDefault();
                    if (top?.Type == ClipboardType.Text && top.Text == text)
                    {
                        return;
                    }

                    var item = new ClipboardItem
                    {
                        Type = ClipboardType.Text,
                        Text = text,
                        Timestamp = DateTime.Now
                    };
                    AddItem(item);
                    return;
                }
            }
        }
        catch
        {
            // Ignore capture failure (e.g. locked clipboard)
        }
    }

    private void AddItem(ClipboardItem item)
    {
        History.Insert(0, item);
        TrimHistory(currentModel.MaxHistoryCount);
        SaveHistory();
        HistoryChanged?.Invoke();
    }

    public void RemoveItem(ClipboardItem item)
    {
        if (History.Remove(item))
        {
            if (item.Type == ClipboardType.Image && !string.IsNullOrEmpty(item.ImagePath) && File.Exists(item.ImagePath))
            {
                try { File.Delete(item.ImagePath); } catch { }
            }
            SaveHistory();
            HistoryChanged?.Invoke();
        }
    }

    public void ClearAll()
    {
        History.Clear();
        try
        {
            if (Directory.Exists(cacheDir))
            {
                foreach (var file in Directory.GetFiles(cacheDir))
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }
        catch { }

        SaveHistory();
        HistoryChanged?.Invoke();
    }

    public bool CopyToClipboard(ClipboardItem item)
    {
        bool success = false;
        try
        {
            switch (item.Type)
            {
                case ClipboardType.Text:
                    if (!string.IsNullOrEmpty(item.Text))
                    {
                        success = ClipboardNative.SetText(item.Text);
                    }
                    break;

                case ClipboardType.Files:
                    if (item.FilePaths != null && item.FilePaths.Length > 0)
                    {
                        success = ClipboardNative.SetFiles(item.FilePaths);
                    }
                    break;

                case ClipboardType.Image:
                    if (!string.IsNullOrEmpty(item.ImagePath) && File.Exists(item.ImagePath))
                    {
                        success = ClipboardNative.SetImageFromFile(item.ImagePath);
                    }
                    break;
            }

            if (success)
            {
                lastSelfSequence = ClipboardNative.GetClipboardSequenceNumber();
                lastSequence = lastSelfSequence;
            }
        }
        catch
        {
            success = false;
        }

        return success;
    }

    private void TrimHistory(int maxCount)
    {
        while (History.Count > maxCount)
        {
            var last = History[^1];
            History.RemoveAt(History.Count - 1);
            if (last.Type == ClipboardType.Image && !string.IsNullOrEmpty(last.ImagePath) && File.Exists(last.ImagePath))
            {
                try { File.Delete(last.ImagePath); } catch { }
            }
        }
    }

    private void LoadHistory()
    {
        if (!File.Exists(historyFilePath)) return;

        try
        {
            string json = File.ReadAllText(historyFilePath);
            var list = JsonSerializer.Deserialize<List<ClipboardItem>>(json);
            if (list == null) return;

            History.Clear();
            foreach (var item in list)
            {
                if (item.Type == ClipboardType.Image && !string.IsNullOrEmpty(item.ImagePath) && File.Exists(item.ImagePath))
                {
                    try
                    {
                        item.Thumbnail = new Bitmap(item.ImagePath);
                    }
                    catch { }
                }
                History.Add(item);
            }
        }
        catch
        {
            // Ignore corrupted history file
        }
    }

    private void SaveHistory()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = false };
            string json = JsonSerializer.Serialize(History.Take(currentModel.MaxHistoryCount).ToList(), options);
            File.WriteAllText(historyFilePath, json);
        }
        catch
        {
            // Ignore write error
        }
    }
}
