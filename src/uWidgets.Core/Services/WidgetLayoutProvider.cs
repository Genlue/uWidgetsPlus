using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;

namespace uWidgets.Core.Services;

/// <inheritdoc cref="IWidgetLayoutProvider" />
public class WidgetLayoutProvider(ILayoutProvider layoutProvider, string screenId, WidgetLayout? widgetLayout) : IWidgetLayoutProvider
{
    /// <inheritdoc />
    public string ScreenId { get; set; } = screenId;

    /// <inheritdoc />
    public event DataChangedEvent<WidgetLayout>? DataChanging;

    /// <inheritdoc />
    public event DataChangedEvent<WidgetLayout>? DataChanged;

    /// <inheritdoc />
    public WidgetLayout Get() => widgetLayout!;

    /// <inheritdoc />
    public void Save(WidgetLayout data)
    {
        DataChanging?.Invoke(this, widgetLayout, data);
        var screens = layoutProvider.Get();
        var screen = screens.FindById(ScreenId);
        if (screen == null) return;

        var layout = screen.Layout;
        var index = layout.IndexOf(widgetLayout!);

        layout = index switch
        {
            -1 => [.. layout, data], // not present on disk yet (first save of a new widget)
            _ => layout.Select((item, i) => i == index ? data : item).ToList()
        };

        layoutProvider.Save(screens.WithScreen(screen with { Layout = layout }));
        var oldData = widgetLayout;
        widgetLayout = data;
        DataChanged?.Invoke(this, oldData, data);
    }

    /// <inheritdoc />
    public void Remove()
    {
        var screens = layoutProvider.Get();
        var screen = screens.FindById(ScreenId);
        if (screen == null) return;

        layoutProvider.Save(screens.WithScreen(screen with
        {
            Layout = screen.Layout.Where(item => item != widgetLayout).ToList()
        }));
    }
}