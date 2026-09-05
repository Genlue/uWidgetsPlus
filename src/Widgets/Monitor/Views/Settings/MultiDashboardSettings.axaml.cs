using Avalonia.Controls;
using Monitor.ViewModels;
using uWidgets.Core.Interfaces;

namespace Monitor.Views.Settings;

public partial class MultiDashboardSettings : UserControl
{
    public MultiDashboardSettings(IWidgetLayoutProvider widgetLayoutProvider)
    {
        DataContext = new MultiDashboardSettingsViewModel(widgetLayoutProvider);
        InitializeComponent();
    }
}
