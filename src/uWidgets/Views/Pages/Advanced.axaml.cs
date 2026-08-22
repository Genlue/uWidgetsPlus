using Avalonia.Controls;
using Avalonia.Interactivity;
using uWidgets.Core.Interfaces;
using uWidgets.ViewModels;
using uWidgets.Views;

namespace uWidgets.Views.Pages;

public partial class Advanced : UserControl
{
    private readonly IAppSettingsProvider appSettingsProvider;

    public Advanced(IAppSettingsProvider appSettingsProvider)
    {
        this.appSettingsProvider = appSettingsProvider;
        DataContext = new AdvancedViewModel(appSettingsProvider);
        InitializeComponent();
    }

    private void OnEditGridClicked(object? sender, RoutedEventArgs e)
    {
        new GridEditor(appSettingsProvider).Show();
    }
}
