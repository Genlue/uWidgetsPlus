using Avalonia.Controls;
using Weather.Models;
using Weather.ViewModels;

namespace Weather.Views;

public partial class AirQuality : UserControl
{
    public AirQuality() : this(new ForecastModel("Beijing", 39.9042, 116.4074, "celsius")) {}

    public AirQuality(ForecastModel model)
    {
        DataContext = new AirQualityViewModel(model);
        InitializeComponent();
    }
}