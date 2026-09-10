using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Music.Models;
using Music.ViewModels;
using Music.Views.Controls;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;
using uWidgets.Services;

namespace Music.Views;

public partial class Music : UserControl, IWidgetSelfRefreshing
{
    private readonly MusicViewModel viewModel;
    private WidgetTier currentTier = (WidgetTier)(-1);

    public Music() : this(new MusicModel()) { }

    public Music(MusicModel model)
    {
        viewModel = new MusicViewModel(model);
        DataContext = viewModel;
        InitializeComponent();

        SizeChanged += OnSizeChanged;
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        SizeChanged -= OnSizeChanged;
        Unloaded -= OnUnloaded;
        viewModel.Dispose();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var size = e.NewSize;
        if (size.Width <= 0 || size.Height <= 0) return;

        // Resolve tier:
        // - Small (2×2): compact, roughly square (<= 220px in both dimensions)
        // - Medium (4×2): wide row (width > 220 && height <= 220, or aspect ratio > 1.35)
        // - Large (4×4): large card (> 220px in both dimensions)
        WidgetTier tier;
        if (size.Width > 220 && size.Height > 220)
        {
            tier = WidgetTier.Large;
        }
        else if (size.Width > size.Height * 1.35 || (size.Width > 220 && size.Height <= 220))
        {
            tier = WidgetTier.Medium;
        }
        else
        {
            tier = WidgetTier.Small;
        }

        if (tier == currentTier && TierContainer.Content != null)
            return;

        currentTier = tier;

        TierContainer.Content = tier switch
        {
            WidgetTier.Large => new MusicLarge(viewModel),
            WidgetTier.Medium => new MusicMedium(viewModel),
            _ => new MusicSmall(viewModel)
        };
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
