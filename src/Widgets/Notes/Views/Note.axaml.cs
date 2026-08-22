using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Notes.Locales;
using Notes.Models;
using Notes.ViewModels;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;

namespace Notes.Views;

public partial class Note : UserControl, IWidgetSelfRefreshing
{
    private readonly IWidgetLayoutProvider widgetLayoutProvider;
    private readonly NoteViewModel viewModel;
    
    public Note(IWidgetLayoutProvider widgetLayoutProvider) 
        : this(new NoteModel(Locale.Notes_Title), widgetLayoutProvider) {}
    
    public Note(NoteModel model, IWidgetLayoutProvider widgetLayoutProvider)
    {
        this.widgetLayoutProvider = widgetLayoutProvider;
        viewModel = new NoteViewModel(model);
        DataContext = viewModel;
        
        InitializeComponent();
    }

    /// <inheritdoc />
    public void Refresh(WidgetLayout layout)
    {
        var newModel = layout.GetModel<NoteModel>();
        if (newModel == null) return;
        viewModel.Update(newModel);
    }

    private void UpdateContent(object? sender, RoutedEventArgs e)
    {
        var newText = (sender as TextBox)!.Text;
        UpdateModel(viewModel.Model with { Content = newText, Updated = DateTime.Now });
    }
    
    private void UpdateTitle(object? sender, RoutedEventArgs e)
    {
        var newText = (sender as TextBox)!.Text;
        UpdateModel(viewModel.Model with { Title = newText, Updated = DateTime.Now });
    }

    private void UpdateModel(NoteModel newModel)
    {
        viewModel.Update(newModel);
        var newSettings = JsonSerializer.SerializeToElement(newModel);
        var newLayout = widgetLayoutProvider.Get() with { Settings = newSettings };
        
        widgetLayoutProvider.Save(newLayout);
    }
}
