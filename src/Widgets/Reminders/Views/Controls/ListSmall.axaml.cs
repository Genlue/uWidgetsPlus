using Avalonia.Controls;
using Avalonia.Interactivity;
using Reminders.ViewModels;

namespace Reminders.Views.Controls;

public partial class ListSmall : UserControl
{
    private readonly List owner;

    public ListSmall(List owner, RemindersViewModel viewModel)
    {
        this.owner = owner;
        DataContext = viewModel;
        InitializeComponent();
    }

    public void ListNameChanged(object? sender, RoutedEventArgs e) => owner.ListNameChanged(sender, e);
    public void CompleteReminder(object? sender, RoutedEventArgs e) => owner.CompleteReminder(sender, e);
    public void EditReminder(object? sender, RoutedEventArgs e) => owner.EditReminder(sender, e);
    public void CreateReminder(object? sender, RoutedEventArgs e) => owner.CreateReminder(sender, e);
}
