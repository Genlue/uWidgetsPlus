using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace Picture.Services;

public sealed class PictureFrame : IDisposable
{
    public Bitmap Bitmap { get; }
    public int DurationMs { get; }

    public PictureFrame(Bitmap bitmap, int durationMs)
    {
        Bitmap = bitmap;
        DurationMs = Math.Max(20, durationMs);
    }

    public void Dispose()
    {
        Bitmap.Dispose();
    }
}

public sealed class DecodedPicture : IDisposable
{
    public bool IsAnimated => Frames.Count > 1;
    public IReadOnlyList<PictureFrame> Frames { get; }
    public Bitmap PrimaryBitmap => Frames.Count > 0 ? Frames[0].Bitmap : null!;

    public DecodedPicture(IReadOnlyList<PictureFrame> frames)
    {
        Frames = frames ?? [];
    }

    public void Dispose()
    {
        foreach (var frame in Frames)
        {
            frame.Dispose();
        }
    }
}

public static class PictureImageLoader
{
    private const int MaxAnimatedFrames = 240;

    public static DecodedPicture? Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            using var stream = File.OpenRead(path);
            using var codec = SKCodec.Create(stream);

            if (codec != null && codec.Info.Width > 0 && codec.Info.Height > 0)
            {
                int frameCount = codec.FrameCount;
                if (frameCount > 1)
                {
                    var animatedFrames = DecodeAnimatedFrames(codec, frameCount);
                    if (animatedFrames.Count > 0)
                    {
                        return new DecodedPicture(animatedFrames);
                    }
                }
                else
                {
                    var singleFrame = DecodeSingleFrame(codec, 0);
                    if (singleFrame != null)
                    {
                        return new DecodedPicture([new PictureFrame(singleFrame, 1000)]);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PictureImageLoader] Skia codec failed for '{path}': {ex.Message}");
        }

        // Fallback 1: Avalonia native Bitmap decoder
        try
        {
            var avaloniaBmp = new Bitmap(path);
            return new DecodedPicture([new PictureFrame(avaloniaBmp, 1000)]);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PictureImageLoader] Avalonia Bitmap fallback failed for '{path}': {ex.Message}");
        }

        // Fallback 2: Skia SKBitmap decoder
        try
        {
            using var skBmp = SKBitmap.Decode(path);
            if (skBmp != null && skBmp.Width > 0 && skBmp.Height > 0)
            {
                var converted = ConvertSkBitmapToWriteable(skBmp);
                if (converted != null)
                {
                    return new DecodedPicture([new PictureFrame(converted, 1000)]);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PictureImageLoader] SKBitmap.Decode fallback failed for '{path}': {ex.Message}");
        }

        return null;
    }

    private static List<PictureFrame> DecodeAnimatedFrames(SKCodec codec, int totalFrames)
    {
        var frames = new List<PictureFrame>();
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        int count = Math.Min(totalFrames, MaxAnimatedFrames);

        var intermediateSkBitmaps = new List<SKBitmap>(count);

        try
        {
            for (int i = 0; i < count; i++)
            {
                var frameInfo = codec.FrameInfo[i];
                int duration = frameInfo.Duration;
                if (duration <= 10) duration = 100; // Standard GIF fallback for 0 or <=10ms

                var skBitmap = new SKBitmap(info);

                int req = frameInfo.RequiredFrame;
                if (req >= 0 && req < intermediateSkBitmaps.Count)
                {
                    intermediateSkBitmaps[req].CopyTo(skBitmap);
                    var options = new SKCodecOptions(i, req);
                    codec.GetPixels(info, skBitmap.GetPixels(), options);
                }
                else
                {
                    skBitmap.Erase(SKColors.Transparent);
                    var options = new SKCodecOptions(i);
                    codec.GetPixels(info, skBitmap.GetPixels(), options);
                }

                intermediateSkBitmaps.Add(skBitmap);

                var wb = ConvertSkBitmapToWriteable(skBitmap);
                if (wb != null)
                {
                    frames.Add(new PictureFrame(wb, duration));
                }
            }
        }
        finally
        {
            foreach (var sk in intermediateSkBitmaps)
            {
                sk.Dispose();
            }
        }

        return frames;
    }

    private static Bitmap? DecodeSingleFrame(SKCodec codec, int frameIndex)
    {
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var skBitmap = new SKBitmap(info);
        skBitmap.Erase(SKColors.Transparent);

        var options = new SKCodecOptions(frameIndex);
        var result = codec.GetPixels(info, skBitmap.GetPixels(), options);

        return ConvertSkBitmapToWriteable(skBitmap);
    }

    private static WriteableBitmap? ConvertSkBitmapToWriteable(SKBitmap skBitmap)
    {
        if (skBitmap.Width <= 0 || skBitmap.Height <= 0) return null;

        var wb = new WriteableBitmap(
            new PixelSize(skBitmap.Width, skBitmap.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using (var locked = wb.Lock())
        {
            unsafe
            {
                if (skBitmap.ColorType == SKColorType.Bgra8888 && skBitmap.AlphaType == SKAlphaType.Premul)
                {
                    Buffer.MemoryCopy(
                        (void*)skBitmap.GetPixels(),
                        (void*)locked.Address,
                        locked.RowBytes * skBitmap.Height,
                        skBitmap.RowBytes * skBitmap.Height);
                }
                else
                {
                    var targetInfo = new SKImageInfo(skBitmap.Width, skBitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                    using var converted = new SKBitmap(targetInfo);
                    skBitmap.CopyTo(converted, SKColorType.Bgra8888);
                    Buffer.MemoryCopy(
                        (void*)converted.GetPixels(),
                        (void*)locked.Address,
                        locked.RowBytes * skBitmap.Height,
                        converted.RowBytes * skBitmap.Height);
                }
            }
        }

        return wb;
    }
}
