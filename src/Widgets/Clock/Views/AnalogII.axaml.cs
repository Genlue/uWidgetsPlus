using Avalonia.Controls;
using Clock.Models;
using Clock.ViewModels;

namespace Clock.Views;

public partial class AnalogII : UserControl
{
    private readonly AnalogClockViewModel viewModel;

    public AnalogII() : this(new ClockModel()) {}
    
    public AnalogII(ClockModel clockModel) 
    {
        viewModel = new AnalogClockViewModel(clockModel);
        DataContext = viewModel;
        Loaded += (_, _) => viewModel.Start();
        Unloaded += (_, _) => viewModel.Stop();
        InitializeComponent();
    }
}