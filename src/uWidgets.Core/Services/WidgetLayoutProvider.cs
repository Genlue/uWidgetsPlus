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
        var index = ResolveIndex(layout);

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

        var index = ResolveIndex(screen.Layout);
        var layout = index == -1
            ? screen.Layout
            : screen.Layout.Where((_, i) => i != index).ToList();

        layoutProvider.Save(screens.WithScreen(screen with { Layout = layout }));
    }

    /// <summary>
    /// Locate this provider's widget inside the screen's layout list.
    /// <para>
    /// <see cref="List{T}.IndexOf"/> and <c>!=</c> on <see cref="WidgetLayout"/> are unreliable
    /// because the record's value equality compares the <see cref="WidgetLayout.Settings"/>
    /// <see cref="System.Text.Json.JsonElement"/> by document reference — two entries parsed
    /// from different documents never compare equal even with identical content. When the
    /// provider's cached instance is not the exact object living in the freshly-read list
    /// (legacy migration, screen hot-plug, ownership transfer), that made every save append a
    /// duplicate "ghost" entry while the original (stale) one kept rendering. Match by
    /// reference first, then by identity (type + geometry, ignoring the settings element).
    /// </para>
    /// </summary>
    private int ResolveIndex(System.Collections.Generic.List<WidgetLayout> layout)
    {
        var current = widgetLayout;
        if (current == null) return -1;

        for (var i = 0; i < layout.Count; i++)
            if (ReferenceEquals(layout[i], current)) return i;

        for (var i = 0; i < layout.Count; i++)
            if (layout[i].SameWidgetAs(current)) return i;

        return -1;
    }
}