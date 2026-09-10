using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models.Settings;

namespace uWidgets.Views;

/// <summary>
/// Manual wallpaper alignment: when the display layer's own wallpaper layout
/// cannot be trusted (taskbar replacements like myDockFinder, wallpaper engines,
/// DWM quirks), the user nudges X/Y until the glass background matches the real
/// desktop behind each widget. Every change is saved immediately and re-renders
/// all liquid-glass surfaces live.
/// </summary>
public partial class WallpaperAlignDialog : Window
{
    private readonly IAppSettingsProvider appSettingsProvider;
    private bool ready;

    public WallpaperAlignDialog(IAppSettingsProvider appSettingsProvider)
    {
        this.appSettingsProvider = appSettingsProvider;
        InitializeComponent();
        var glass = appSettingsProvider.Get().Theme.EffectiveLiquidGlass;
        OffsetX.Value = (decimal)glass.WallpaperOffsetX;
        OffsetY.Value = (decimal)glass.WallpaperOffsetY;
        ready = true;
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
        };
    }

    private void OnOffsetChanged(object? sender, Avalonia.Controls.NumericUpDownValueChangedEventArgs e)
    {
        if (!ready) return;
        ApplyOffsets();
    }

    private void ApplyOffsets()
    {
        var settings = appSettingsProvider.Get();
        var glass = settings.Theme.EffectiveLiquidGlass;
        var next = (glass with
        {
            WallpaperOffsetX = (double)(OffsetX.Value ?? 0),
            WallpaperOffsetY = (double)(OffsetY.Value ?? 0)
        }).Normalize();
        appSettingsProvider.Save(settings with { Theme = settings.Theme with { LiquidGlass = next } });
    }

    private void ResetOffsets(object? sender, RoutedEventArgs e)
    {
        ready = false;
        OffsetX.Value = 0;
        OffsetY.Value = 0;
        ready = true;
        ApplyOffsets();
    }

    private void CloseDialog(object? sender, RoutedEventArgs e) => Close();

    // Drag by the blank areas only; buttons and the steppers keep their clicks.
    private void Drag(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Button or NumericUpDown) return;
        BeginMoveDrag(e);
    }
}
