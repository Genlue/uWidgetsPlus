using System;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Music.Models;
using Music.ViewModels;
using Music.Views.Controls;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;

namespace Music.Views;

public enum MusicTier
{
    Cell1x1,
    Banner4x1,
    Small2x2,
    Medium4x2,
    Large4x4
}

public partial class Music : UserControl, IWidgetSelfRefreshing
{
    private readonly MusicViewModel viewModel;
    private MusicTier currentTier = (MusicTier)(-1);

    public Music() : this(new MusicModel()) { }

    public Music(MusicModel model)
    {
        viewModel = new MusicViewModel(model);
        DataContext = viewModel;
        InitializeComponent();

        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        UpdateLayoutTier(Bounds.Size);
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateLayoutTier(e.NewSize);
    }

    public void UpdateLayoutTier(Size size)
    {
        if (size.Width <= 0 || size.Height <= 0) return;

        var tier = ResolveTier(size);

        if (tier != currentTier || TierContainer.Content == null)
        {
            currentTier = tier;
            TierContainer.Content = tier switch
            {
                MusicTier.Cell1x1 => new MusicCell(viewModel),
                MusicTier.Banner4x1 => new MusicBanner(viewModel),
                MusicTier.Small2x2 => new MusicSmall(viewModel),
                MusicTier.Large4x4 => new MusicLarge(viewModel),
                _ => new MusicMedium(viewModel)
            };
        }

        // Trigger adaptive layout on active content
        switch (TierContainer.Content)
        {
            case MusicCell cell:
                cell.AdaptLayout(size);
                break;
            case MusicBanner banner:
                banner.AdaptLayout(size);
                break;
            case MusicSmall small:
                small.AdaptLayout(size);
                break;
            case MusicMedium medium:
                medium.AdaptLayout(size);
                break;
            case MusicLarge large:
                large.AdaptLayout(size);
                break;
        }
    }

    public MusicTier ResolveTier(Size size)
    {
        var span = FindHostSpan();
        if (span != null)
        {
            var (cols, rows) = span.Value;
            if (cols <= 1 && rows <= 1) return MusicTier.Cell1x1;
            if (rows == 1) return MusicTier.Banner4x1;
            if (cols <= 2 && rows <= 2) return MusicTier.Small2x2;
            if (rows >= 3 && cols >= 3) return MusicTier.Large4x4;
            return MusicTier.Medium4x2;
        }

        // Robust pixel-based fallback:
        // 1x1 cell: tiny square card
        if (size.Width <= 100 && size.Height <= 100)
            return MusicTier.Cell1x1;

        // 1-row banner: wide but height <= 95
        if (size.Height <= 95 && size.Width >= 130)
            return MusicTier.Banner4x1;

        // 2x2 small: roughly square card <= 210
        if (size.Width <= 210 && size.Height <= 210 && Math.Abs(size.Width - size.Height) <= 50)
            return MusicTier.Small2x2;

        // 4x4 large: both dimensions large (>= 250)
        if (size.Width >= 250 && size.Height >= 250)
            return MusicTier.Large4x4;

        // Default to 4x2 / 3x2 medium
        return MusicTier.Medium4x2;
    }

    private (int Columns, int Rows)? FindHostSpan()
    {
        for (var node = this.GetVisualParent(); node != null; node = node.GetVisualParent())
            if (node is uWidgets.Views.Widget widget)
                return widget.CurrentSpan;
        return null;
    }

    public void Refresh(WidgetLayout layout)
    {
        if (layout.Settings is not { } settings || settings.ValueKind != JsonValueKind.Object)
            return;

        try
        {
            var newModel = settings.Deserialize<MusicModel>();
            if (newModel != null)
            {
                viewModel.UpdateModel(newModel);
            }
        }
        catch (JsonException) { }
    }
}
