using Avalonia.Controls;
using Calendar.ViewModels;

namespace Calendar.Views;

public partial class Date : UserControl
{
    private readonly DateCalendarViewModel viewModel;

    public Date()
    {
        viewModel = new DateCalendarViewModel();
        DataContext = viewModel;
        Loaded += (_, _) => viewModel.Start();
        Unloaded += (_, _) => viewModel.Stop();
        InitializeComponent();
    }
}