using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Reminders.ViewModels;

namespace Reminders.Views.Controls;

public partial class ListSmall : UserControl
{
    private readonly List owner;

    public ListSmall(List owner, RemindersViewModel viewModel, bool compact = false)
    {
        this.owner = owner;
        DataContext = viewModel;
        InitializeComponent();

        // S tier (1×1): the editable list-name header and the counter eat into the
        // few rows that fit — drop them and let the items own the card (the name
        // stays editable in the widget's own settings dialog).
        if (compact)
        {
            ListNameBox.IsVisible = false;
            CountText.IsVisible = false;
            Margin = new Thickness(8, 6, 4, 0);
        }
    }

    public void ListNameChanged(object? sender, RoutedEventArgs e) => owner.ListNameChanged(sender, e);
    public void CompleteReminder(object? sender, RoutedEventArgs e) => owner.CompleteReminder(sender, e);
    public void EditReminder(object? sender, RoutedEventArgs e) => owner.EditReminder(sender, e);
    public void CreateReminder(object? sender, RoutedEventArgs e) => owner.CreateReminder(sender, e);
}
