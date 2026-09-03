using Avalonia.Controls;
using Weather.Models;
using Weather.ViewModels;

namespace Weather.Views;

public partial class Temperature : UserControl
{
    public Temperature() : this(new ForecastModel("Beijing", 39.9042, 116.4074, "celsius")) {}

    public Temperature(ForecastModel model)
    {
        DataContext = new ForecastViewModel(model);
        InitializeComponent();
    }
}