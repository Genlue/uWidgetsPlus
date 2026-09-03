using Avalonia.Controls;
using Weather.Models;
using Weather.ViewModels;

namespace Weather.Views;

public partial class UVIndex : UserControl
{
    public UVIndex() : this(new ForecastModel("Beijing", 39.9042, 116.4074, "celsius")) {}

    public UVIndex(ForecastModel model)
    {
        DataContext = new ForecastViewModel(model);
        InitializeComponent();
    }
}