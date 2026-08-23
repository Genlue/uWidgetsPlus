using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Reminders.Locales;
using Reminders.Models;
using Reminders.ViewModels;
using Reminders.Views.Controls;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;

namespace Reminders.Views;

public partial class List : UserControl, IWidgetSelfRefreshing
{
    private readonly IWidgetLayoutProvider widgetLayoutProvider;
    private readonly RemindersViewModel viewModel;

    public List(IWidgetLayoutProvider widgetLayoutProvider) 
        : this(new RemindersListModel(Locale.Reminders_List_Title, []), widgetLayoutProvider) {}
    
    public List(RemindersListModel model, IWidgetLayoutProvider widgetLayoutProvider)
    {
        this.widgetLayoutProvider = widgetLayoutProvider;
        viewModel = new RemindersViewModel(model);
        Content = new ListSmall(this, viewModel);
        SizeChanged += OnSizeChanged;
        Unloaded += OnUnloaded;
        InitializeComponent();
    }

    /// <inheritdoc />
    public void Refresh(WidgetLayout layout)
    {
        var newModel = layout.GetModel<RemindersListModel>();
        if (newModel == null) return;
        viewModel.Update(newModel);
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        SizeChanged -= OnSizeChanged;
        Unloaded -= OnUnloaded;
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        const int smallSize = 200;
        var small = e.NewSize is { Width: < smallSize, Height: < smallSize };
        var wide = e.NewSize.AspectRatio >= 1.5;
        
        Content = (small, wide) switch
        {
            (true, _) => new ListSmall(this, viewModel),
            (_, true) => new ListWide(this, viewModel),
            _ => new ListLarge(this, viewModel)
        };
    }

    public void ListNameChanged(object? sender, RoutedEventArgs e)
    {
        var listName = (sender as TextBox)!.Text;
        UpdateModel(viewModel.Model with { ListName = listName });
    }

    private void UpdateModel(RemindersListModel newModel)
    {
        viewModel.Update(newModel);
        var newSettings = JsonSerializer.SerializeToElement(newModel);
        var newLayout = widgetLayoutProvider.Get() with { Settings = newSettings };
        
        widgetLayoutProvider.Save(newLayout);
    }

    public void CompleteReminder(object? sender, RoutedEventArgs e)
    {
        var reminder = (sender as Button)!.DataContext as ReminderModel;
        var index = viewModel.Reminders.IndexOf(reminder!);
        if (index < 0) return;

        viewModel.Reminders[index] = viewModel.Reminders[index] with { Completed = !viewModel.Reminders[index].Completed };
        UpdateModel(viewModel.Model);
    }
    
    public void EditReminder(object? sender, RoutedEventArgs e)
    {
        var text = (sender as TextBox)!.Text;
        var reminder = (sender as TextBox)!.DataContext as ReminderModel;
        var index = viewModel.Reminders.IndexOf(reminder!);
        if (index < 0) return;

        if (string.IsNullOrEmpty(text))
            viewModel.Reminders.RemoveAt(index);
        else
            viewModel.Reminders[index] = viewModel.Reminders[index] with { Title = text };
        
        UpdateModel(viewModel.Model);
    }
    
    public void CreateReminder(object? sender, RoutedEventArgs e)
    {
        var text = (sender as TextBox)!.Text;
        
        if (string.IsNullOrEmpty(text))
            return;

        viewModel.Reminders.Add(new ReminderModel(false, text));

        (sender as TextBox)!.Clear();
        UpdateModel(viewModel.Model);
    }
}
