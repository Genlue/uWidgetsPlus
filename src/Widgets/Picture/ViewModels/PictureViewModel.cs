using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Picture.Models;
using Picture.Services;
using ReactiveUI;

namespace Picture.ViewModels;

public class PictureViewModel : ReactiveObject, IDisposable
{
    private PictureModel model;
    private readonly DispatcherTimer slideshowTimer;
    private readonly DispatcherTimer transitionTimer;
    private readonly DispatcherTimer gifAnimationTimer;

    private readonly Dictionary<string, DecodedPicture> pictureCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Random random = new();
    private double currentTransitionStep;
    private DecodedPicture? activePicture;
    private int currentGifFrameIndex;

    public PictureViewModel(PictureModel model)
    {
        this.model = model;

        slideshowTimer = new DispatcherTimer();
        slideshowTimer.Tick += OnSlideshowTick;

        transitionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        transitionTimer.Tick += OnTransitionTick;

        gifAnimationTimer = new DispatcherTimer();
        gifAnimationTimer.Tick += OnGifAnimationTick;

        ApplyModel(model);
    }

    public void ApplyModel(PictureModel newModel)
    {
        model = newModel;
        ConfigureSlideshowTimer();
        LoadCurrentPicture(immediate: true);
        this.RaisePropertyChanged(nameof(ShowCaption));
        this.RaisePropertyChanged(nameof(CornerRadius));
    }

    private void ConfigureSlideshowTimer()
    {
        slideshowTimer.Stop();

        if (model.Interval == SlideshowInterval.Manual || model.Order == PlayOrder.Fixed)
            return;

        var span = model.Interval switch
        {
            SlideshowInterval.Seconds5 => TimeSpan.FromSeconds(5),
            SlideshowInterval.Seconds10 => TimeSpan.FromSeconds(10),
            SlideshowInterval.Seconds30 => TimeSpan.FromSeconds(30),
            SlideshowInterval.Minutes1 => TimeSpan.FromMinutes(1),
            SlideshowInterval.Minutes5 => TimeSpan.FromMinutes(5),
            SlideshowInterval.Minutes15 => TimeSpan.FromMinutes(15),
            SlideshowInterval.Minutes30 => TimeSpan.FromMinutes(30),
            SlideshowInterval.Hours1 => TimeSpan.FromHours(1),
            SlideshowInterval.Days1 => TimeSpan.FromDays(1),
            _ => TimeSpan.FromMinutes(5)
        };

        slideshowTimer.Interval = span;
        slideshowTimer.Start();
    }

    private void OnSlideshowTick(object? sender, EventArgs e)
    {
        NextPicture();
    }

    public void NextPicture()
    {
        var items = model.GetItems();
        if (items.Count <= 1) return;

        int nextIndex;
        if (model.Order == PlayOrder.Shuffle)
        {
            do
            {
                nextIndex = random.Next(items.Count);
            } while (nextIndex == model.CurrentIndex && items.Count > 1);
        }
        else
        {
            nextIndex = (model.CurrentIndex + 1) % items.Count;
        }

        model = model with { CurrentIndex = nextIndex };
        LoadCurrentPicture(immediate: false);
    }

    public void PreviousPicture()
    {
        var items = model.GetItems();
        if (items.Count <= 1) return;

        int prevIndex = (model.CurrentIndex - 1 + items.Count) % items.Count;
        model = model with { CurrentIndex = prevIndex };
        LoadCurrentPicture(immediate: false);
    }

    private void OnGifAnimationTick(object? sender, EventArgs e)
    {
        if (activePicture == null || !activePicture.IsAnimated || activePicture.Frames.Count <= 1)
        {
            gifAnimationTimer.Stop();
            return;
        }

        currentGifFrameIndex = (currentGifFrameIndex + 1) % activePicture.Frames.Count;
        var frame = activePicture.Frames[currentGifFrameIndex];
        CurrentBitmap = frame.Bitmap;
        gifAnimationTimer.Interval = TimeSpan.FromMilliseconds(frame.DurationMs);
    }

    private DecodedPicture? GetOrLoadPicture(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        if (pictureCache.TryGetValue(path, out var cached))
            return cached;

        var decoded = PictureImageLoader.Load(path);
        if (decoded != null)
        {
            if (pictureCache.Count >= 16)
            {
                var enumerator = pictureCache.Keys.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    var key = enumerator.Current;
                    if (pictureCache.Remove(key, out var oldPic) && oldPic != activePicture)
                    {
                        oldPic.Dispose();
                    }
                }
            }

            pictureCache[path] = decoded;
            return decoded;
        }

        return null;
    }

    private void LoadCurrentPicture(bool immediate)
    {
        var items = model.GetItems();
        if (items.Count == 0)
        {
            gifAnimationTimer.Stop();
            activePicture = null;
            CurrentBitmap = null;
            PreviousBitmap = null;
            TransitionProgress = 1.0;
            HasPictures = false;
            Caption = null;
            return;
        }

        int index = Math.Clamp(model.CurrentIndex, 0, items.Count - 1);
        var item = items[index];

        var pic = GetOrLoadPicture(item.Path);
        if (pic == null)
        {
            // Try fallback to any valid picture
            foreach (var fallback in items)
            {
                pic = GetOrLoadPicture(fallback.Path);
                if (pic != null)
                {
                    item = fallback;
                    break;
                }
            }
        }

        activePicture = pic;
        gifAnimationTimer.Stop();
        currentGifFrameIndex = 0;

        HasPictures = pic != null;
        Caption = !string.IsNullOrWhiteSpace(item.Name) ? item.Name : Path.GetFileNameWithoutExtension(item.Path);
        CropX = item.CropX;
        CropY = item.CropY;
        Zoom = item.Zoom;
        FitMode = model.FitMode;

        var targetBmp = pic?.PrimaryBitmap;

        if (immediate || CurrentBitmap == null || targetBmp == null)
        {
            transitionTimer.Stop();
            PreviousBitmap = null;
            CurrentBitmap = targetBmp;
            TransitionProgress = 1.0;
        }
        else
        {
            PreviousBitmap = CurrentBitmap;
            CurrentBitmap = targetBmp;
            TransitionProgress = 0.0;
            currentTransitionStep = 0.0;
            transitionTimer.Start();
        }

        if (pic?.IsAnimated == true && pic.Frames.Count > 1)
        {
            gifAnimationTimer.Interval = TimeSpan.FromMilliseconds(pic.Frames[0].DurationMs);
            gifAnimationTimer.Start();
        }
    }

    private void OnTransitionTick(object? sender, EventArgs e)
    {
        currentTransitionStep += 0.05; // 20 steps = ~400ms
        if (currentTransitionStep >= 1.0)
        {
            transitionTimer.Stop();
            TransitionProgress = 1.0;
            PreviousBitmap = null;
        }
        else
        {
            TransitionProgress = currentTransitionStep;
        }
    }

    public void OpenCurrentInShell()
    {
        var items = model.GetItems();
        if (items.Count == 0) return;

        int index = Math.Clamp(model.CurrentIndex, 0, items.Count - 1);
        var path = items[index].Path;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch
            {
                // Ignored
            }
        }
    }

    private Bitmap? currentBitmap;
    public Bitmap? CurrentBitmap
    {
        get => currentBitmap;
        private set => this.RaiseAndSetIfChanged(ref currentBitmap, value);
    }

    private Bitmap? previousBitmap;
    public Bitmap? PreviousBitmap
    {
        get => previousBitmap;
        private set => this.RaiseAndSetIfChanged(ref previousBitmap, value);
    }

    private double transitionProgress = 1.0;
    public double TransitionProgress
    {
        get => transitionProgress;
        private set => this.RaiseAndSetIfChanged(ref transitionProgress, value);
    }

    private double cropX = 0.5;
    public double CropX
    {
        get => cropX;
        private set => this.RaiseAndSetIfChanged(ref cropX, value);
    }

    private double cropY = 0.5;
    public double CropY
    {
        get => cropY;
        private set => this.RaiseAndSetIfChanged(ref cropY, value);
    }

    private double zoom = 1.0;
    public double Zoom
    {
        get => zoom;
        private set => this.RaiseAndSetIfChanged(ref zoom, value);
    }

    private PictureFitMode fitMode = PictureFitMode.CustomCrop;
    public PictureFitMode FitMode
    {
        get => fitMode;
        private set => this.RaiseAndSetIfChanged(ref fitMode, value);
    }

    private bool hasPictures;
    public bool HasPictures
    {
        get => hasPictures;
        private set => this.RaiseAndSetIfChanged(ref hasPictures, value);
    }

    private string? caption;
    public string? Caption
    {
        get => caption;
        private set => this.RaiseAndSetIfChanged(ref caption, value);
    }

    public bool ShowCaption => model.ShowCaption && !string.IsNullOrEmpty(Caption);

    public CornerRadius CornerRadius => model.CornerRadius > 0 ? new CornerRadius(model.CornerRadius) : new CornerRadius(0);

    public bool ClickToNext => model.ClickToNext;
    public bool DoubleClickToOpen => model.DoubleClickToOpen;

    public void Dispose()
    {
        slideshowTimer.Stop();
        transitionTimer.Stop();
        gifAnimationTimer.Stop();

        foreach (var pic in pictureCache.Values)
        {
            pic.Dispose();
        }
        pictureCache.Clear();

        GC.SuppressFinalize(this);
    }
}
