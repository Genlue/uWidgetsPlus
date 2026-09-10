using Avalonia.Controls;
using Avalonia.Interactivity;
using uWidgets.Core.Interfaces;
using uWidgets.ViewModels;

namespace uWidgets.Views.Pages;

public partial class Appearance : UserControl
{
    private readonly IAppSettingsProvider appSettingsProvider;

    public Appearance(IAppSettingsProvider appSettingsProvider)
    {
        this.appSettingsProvider = appSettingsProvider;
        DataContext = new AppearanceViewModel(appSettingsProvider);
        InitializeComponent();
    }

    private void ApplySolidToLight(object? sender, RoutedEventArgs e) =>
        ((AppearanceViewModel)DataContext!).ApplySolidToLight();

    private void ApplySolidToDark(object? sender, RoutedEventArgs e) =>
        ((AppearanceViewModel)DataContext!).ApplySolidToDark();

    private void ResetLiquidGlass(object? sender, RoutedEventArgs e) =>
        ((AppearanceViewModel)DataContext!).ResetLiquidGlass();

    private void RefreshLiquidGlassWallpaper(object? sender, RoutedEventArgs e) =>
        ((AppearanceViewModel)DataContext!).RefreshLiquidGlassWallpaper();

    private void OpenWallpaperAlign(object? sender, RoutedEventArgs e) =>
        new WallpaperAlignDialog(appSettingsProvider).Show();
}
