using Avalonia.Controls;
using Avalonia.Interactivity;
using uWidgets.Core.Interfaces;
using uWidgets.ViewModels;

namespace uWidgets.Views.Pages;

public partial class Appearance : UserControl
{
    public Appearance(IAppSettingsProvider appSettingsProvider)
    {
        DataContext = new AppearanceViewModel(appSettingsProvider);
        InitializeComponent();
    }

    private void ApplySolidToLight(object? sender, RoutedEventArgs e) =>
        ((AppearanceViewModel)DataContext!).ApplySolidToLight();

    private void ApplySolidToDark(object? sender, RoutedEventArgs e) =>
        ((AppearanceViewModel)DataContext!).ApplySolidToDark();
}
