using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Weather.Models;
using Weather.ViewModels;

namespace Weather.Views;

public partial class Pressure : UserControl
{
    public Pressure() : this(new ForecastModel("Beijing", 39.9042, 116.4074, "celsius")) {}

    public Pressure(ForecastModel model)
    {
        DataContext = new ForecastViewModel(model);
        InitializeComponent();
    }
}