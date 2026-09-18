using System;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Batteries.Models;
using Batteries.ViewModels;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;
using uWidgets.Services;

namespace Batteries.Views;

public partial class BatteriesView : UserControl, IWidgetSelfRefreshing
{
    private readonly IWidgetLayoutProvider? layoutProvider;
    private readonly BatteriesViewModel viewModel;
    private WidgetTier currentTier = (WidgetTier)(-1);

    public BatteriesView() : this(new BatteriesModel(), null) { }

    public BatteriesView(IWidgetLayoutProvider layoutProvider) : this(new BatteriesModel(), layoutProvider) { }

    public BatteriesView(BatteriesModel model) : this(model, null) { }

    public BatteriesView(BatteriesModel model, IWidgetLayoutProvider? layoutProvider)
    {
        this.layoutProvider = layoutProvider;
        viewModel = new BatteriesViewModel(model);
        DataContext = viewModel;

        InitializeComponent();

        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Bounds.Width > 0 && Bounds.Height > 0)
        {
            ApplySize(Bounds.Size);
        }
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
            ApplySize(e.NewSize);
        }
    }

    private void ApplySize(Size size)
    {
        var tier = ResolveTier(size);
        if (tier == currentTier) return;
        currentTier = tier;

        bool isWide = tier == WidgetTier.Medium || (size.Width > size.Height * 1.35 && size.Width > 240);
        SmallLayout.IsVisible = !isWide;
        WideLayout.IsVisible = isWide;
    }

    private WidgetTier ResolveTier(Size size)
    {
        try
        {
            var spanTier = SizeTiers.ResolveTier(this, size);
            if (spanTier != WidgetTier.Other) return spanTier;
        }
        catch { }

        if (size.Width >= 260 && size.Width > size.Height * 1.35)
            return WidgetTier.Medium;
        if (size.Width >= 280 && size.Height >= 280)
            return WidgetTier.Large;

        return WidgetTier.Small;
    }

    public void Refresh(WidgetLayout layout)
    {
        if (layout.Settings is not { } settings || settings.ValueKind != JsonValueKind.Object)
            return;

        try
        {
            var updated = settings.Deserialize<BatteriesModel>();
            if (updated != null)
            {
                viewModel.UpdateModel(updated);
            }
        }
        catch { }
    }
}
