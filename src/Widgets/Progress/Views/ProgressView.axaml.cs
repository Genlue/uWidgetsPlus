using System.Text.Json;
using Avalonia.Controls;
using Progress.Models;
using Progress.ViewModels;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;

namespace Progress.Views;

public partial class ProgressView : UserControl, IWidgetSelfRefreshing
{
    private readonly ProgressViewModel viewModel;
    private readonly IWidgetLayoutProvider? widgetLayoutProvider;

    public ProgressView() : this(new ProgressModel()) { }

    public ProgressView(ProgressModel model) : this(model, null, null) { }

    public ProgressView(IWidgetLayoutProvider widgetLayoutProvider)
        : this(new ProgressModel(), widgetLayoutProvider, null) { }

    public ProgressView(ProgressModel model, IWidgetLayoutProvider? widgetLayoutProvider, IAppSettingsProvider? appSettingsProvider)
    {
        this.widgetLayoutProvider = widgetLayoutProvider;
        viewModel = new ProgressViewModel(model, appSettingsProvider);
        DataContext = viewModel;

        InitializeComponent();

        Loaded += (_, _) => viewModel.Start();
        Unloaded += (_, _) => viewModel.Stop();
    }

    public void Refresh(WidgetLayout layout)
    {
        if (layout?.Settings is { } settings && settings.ValueKind == JsonValueKind.Object)
        {
            try
            {
                var newModel = settings.Deserialize<ProgressModel>();
                if (newModel != null)
                {
                    viewModel.UpdateModel(newModel);
                }
            }
            catch
            {
                // Ignored
            }
        }
    }
}
