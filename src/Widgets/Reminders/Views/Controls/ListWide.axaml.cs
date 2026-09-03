using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Reminders.ViewModels;

namespace Reminders.Views.Controls;

public partial class ListWide : UserControl
{
    private readonly List owner;

    public ListWide(List owner, RemindersViewModel viewModel, bool compact = false)
    {
        this.owner = owner;
        DataContext = viewModel;
        InitializeComponent();

        // M tier (2×1): the 90px-wide name column leaves almost nothing for the
        // items — hide the name and shrink the counter to a small badge.
        if (compact)
        {
            ListNameBox.IsVisible = false;
            CountText.FontSize = 20;
            Margin = new Thickness(8, 2, 4, 0);
        }
    }

    public void ListNameChanged(object? sender, RoutedEventArgs e) => owner.ListNameChanged(sender, e);
    public void CompleteReminder(object? sender, RoutedEventArgs e) => owner.CompleteReminder(sender, e);
    public void EditReminder(object? sender, RoutedEventArgs e) => owner.EditReminder(sender, e);
    public void CreateReminder(object? sender, RoutedEventArgs e) => owner.CreateReminder(sender, e);
}
