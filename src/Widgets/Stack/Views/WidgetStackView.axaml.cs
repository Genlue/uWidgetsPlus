using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using StackWidgets.Models;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;
using uWidgets.Core.Models.Attributes;
using uWidgets.Views;

namespace StackWidgets.Views;

public partial class WidgetStackView : UserControl, IWidgetSelfRefreshing, IStackWidget
{
    private readonly IWidgetLayoutProvider widgetLayoutProvider;
    private readonly IAssemblyProvider assemblyProvider;
    private WidgetStackModel model;
    private DispatcherTimer? saveDebounceTimer;
    private bool isSavingSelf;

    private readonly Dictionary<int, UserControl?> childControls = new();
    private int currentActiveIndex = 0;
    private DispatcherTimer? activeTransitionTimer;
    private int transitionTargetIndex = -1;

    public event EventHandler? IndicatorItemsChanged;

    public WidgetStackModel Model => model;

    public bool CanEditCurrentChild => CanEditChildAt(model.SelectedIndex);

    public string? CurrentChildTitle
    {
        get
        {
            if (model?.Entries == null || model.SelectedIndex < 0 || model.SelectedIndex >= model.Entries.Count) return null;
            return model.Entries[model.SelectedIndex].DisplayTitle;
        }
    }

    public IReadOnlyList<StackWidgetIndicatorItem> IndicatorItems
    {
        get
        {
            if (model?.Entries == null || model.Entries.Count <= 1) return Array.Empty<StackWidgetIndicatorItem>();
            var list = new List<StackWidgetIndicatorItem>();
            for (int i = 0; i < model.Entries.Count; i++)
            {
                bool isActive = i == model.SelectedIndex;
                list.Add(new StackWidgetIndicatorItem(
                    i,
                    model.Entries[i].DisplayTitle,
                    isActive,
                    isActive ? 1.0 : 0.30
                ));
            }
            return list;
        }
    }

    public bool IsTransitionActive => activeTransitionTimer != null;

    public WidgetStackView()
        : this(new WidgetStackModel(), null!, null!)
    {
    }

    public WidgetStackView(IWidgetLayoutProvider widgetLayoutProvider, IAssemblyProvider assemblyProvider)
        : this(new WidgetStackModel(), widgetLayoutProvider, assemblyProvider)
    {
    }

    public WidgetStackView(WidgetStackModel model, IWidgetLayoutProvider widgetLayoutProvider, IAssemblyProvider assemblyProvider)
    {
        this.model = model ?? new WidgetStackModel();
        this.widgetLayoutProvider = widgetLayoutProvider;
        this.assemblyProvider = assemblyProvider;

        InitializeComponent();
        Classes.Add("Flush");

        EnsureValidModel();
        RenderCurrentState();

        Loaded += (_, _) => RenderCurrentState();
        Unloaded += (_, _) =>
        {
            if (saveDebounceTimer?.IsEnabled == true)
            {
                saveDebounceTimer.Stop();
                SaveModelDirect();
            }
        };
    }

    private void EnsureValidModel()
    {
        if (model == null)
        {
            model = new WidgetStackModel();
        }

        if (model.Entries == null || model.Entries.Count == 0)
        {
            model = model with { Entries = WidgetStackModel.GetDefaultEntries(), SelectedIndex = 0 };
        }

        if (model.SelectedIndex < 0 || model.SelectedIndex >= model.Entries.Count)
        {
            model = model with { SelectedIndex = Math.Clamp(model.SelectedIndex, 0, Math.Max(0, model.Entries.Count - 1)) };
        }
    }

    public void RenderCurrentState()
    {
        EnsureValidModel();
        UpdateDots();
        EnsureAllChildrenLoaded();
    }

    private void UpdateDots()
    {
        try
        {
            IndicatorItemsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WidgetStack] UpdateDots error: {ex.Message}");
        }
    }

    private void EnsureAllChildrenLoaded()
    {
        EnsureValidModel();
        if (WidgetsHost == null) return;
        if (model?.Entries == null || model.Entries.Count == 0)
        {
            WidgetsHost.Children.Clear();
            return;
        }

        for (int i = 0; i < model.Entries.Count; i++)
        {
            GetOrCreateChildControl(i);
        }

        currentActiveIndex = model.SelectedIndex;
        EnforceOnlyActiveVisible(currentActiveIndex);
    }

    private void EnforceOnlyActiveVisible(int activeIndex)
    {
        if (model?.Entries == null) return;
        for (int i = 0; i < model.Entries.Count; i++)
        {
            if (childControls.TryGetValue(i, out var c) && c != null)
            {
                bool isActive = (i == activeIndex);
                c.IsVisible = isActive;
                c.IsHitTestVisible = isActive;
                c.Opacity = 1.0;
                c.RenderTransform = null;
                c.ZIndex = isActive ? 1 : 0;
            }
        }
    }

    private UserControl? GetOrCreateChildControl(int index)
    {
        if (index < 0 || index >= model.Entries.Count) return null;

        if (!childControls.TryGetValue(index, out var control) || control == null)
        {
            control = CreateChildControl(index, model.Entries[index]);
            if (control != null)
            {
                control.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
                control.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
                childControls[index] = control;
                if (!WidgetsHost.Children.Contains(control))
                {
                    WidgetsHost.Children.Add(control);
                }
            }
        }
        else if (!WidgetsHost.Children.Contains(control))
        {
            WidgetsHost.Children.Add(control);
        }

        return control;
    }

    private UserControl? CreateChildControl(int index, StackedWidgetEntry entry)
    {
        try
        {
            if (assemblyProvider == null) return null;
            var assembly = assemblyProvider.LoadAssembly(entry.AssemblyName);
            var widgetInfo = assembly
                .GetCustomAttributes<WidgetInfoAttribute>()
                .FirstOrDefault(a => a.ViewType.Name == entry.ViewTypeName);

            if (widgetInfo == null) return null;

            object? childModel = null;
            if (widgetInfo.ModelType != null)
            {
                if (!string.IsNullOrEmpty(entry.SettingsJson))
                {
                    try { childModel = JsonSerializer.Deserialize(entry.SettingsJson, widgetInfo.ModelType); } catch { }
                }

                if (childModel == null)
                {
                    try { childModel = Activator.CreateInstance(widgetInfo.ModelType); } catch { }
                }
            }

            var childLayoutProvider = new StackedChildLayoutProvider((e, json) =>
            {
                UpdateChildSettings(index, json, forceRecreateIfNotSelfRefreshing: false);
            }, entry, widgetLayoutProvider);

            List<object> args = [];
            if (NeedsWidgetLayoutProvider(widgetInfo.ViewType))
                args.Add(childLayoutProvider);
            if (childModel != null)
                args.Add(childModel);

            var control = assemblyProvider.Activate(widgetInfo.ViewType, args.ToArray()) as UserControl;
            return control;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WidgetStack] Failed to create child control: {ex.Message}");
            return null;
        }
    }

    private static bool NeedsWidgetLayoutProvider(Type type) =>
        type.GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(IWidgetLayoutProvider)));

    public bool CanEditChildAt(int index)
    {
        if (model?.Entries == null || index < 0 || index >= model.Entries.Count) return false;
        var entry = model.Entries[index];
        return GetEditModelViewType(entry) != null;
    }

    public Type? GetEditModelViewType(StackedWidgetEntry entry)
    {
        try
        {
            if (assemblyProvider == null || string.IsNullOrEmpty(entry.AssemblyName)) return null;
            var assembly = assemblyProvider.LoadAssembly(entry.AssemblyName);
            if (assembly == null) return null;
            var widgetInfo = assembly
                .GetCustomAttributes<WidgetInfoAttribute>()
                .FirstOrDefault(a => a.ViewType?.Name == entry.ViewTypeName);
            return widgetInfo?.EditModelViewType;
        }
        catch
        {
            return null;
        }
    }

    public void EditCurrentChild(object? ownerWindow = null)
    {
        if (model?.Entries == null || model.SelectedIndex < 0 || model.SelectedIndex >= model.Entries.Count) return;
        EditChildAt(model.SelectedIndex, ownerWindow);
    }

    public void EditChildAt(int index, object? ownerWindow = null)
    {
        if (model?.Entries == null || index < 0 || index >= model.Entries.Count) return;
        var entry = model.Entries[index];
        var editType = GetEditModelViewType(entry);
        if (editType == null) return;

        var childLayoutProvider = new StackedChildLayoutProvider((e, json) =>
        {
            UpdateChildSettings(index, json, forceRecreateIfNotSelfRefreshing: true);
        }, entry, widgetLayoutProvider);

        var editControl = (UserControl)assemblyProvider.Activate(editType, childLayoutProvider);
        var editWindow = new EditWidget(childLayoutProvider, editControl);

        if (ownerWindow is Window win)
        {
            editWindow.ShowDialog(win);
        }
        else
        {
            editWindow.Show();
        }
    }

    public void SwitchToIndex(int index)
    {
        try
        {
            if (model?.Entries == null || model.Entries.Count == 0) return;
            if (index < 0 || index >= model.Entries.Count || index == currentActiveIndex) return;

            // Stop any previous running transition
            if (activeTransitionTimer != null)
            {
                activeTransitionTimer.Stop();
                activeTransitionTimer = null;
                if (transitionTargetIndex >= 0 && transitionTargetIndex < model.Entries.Count)
                {
                    EnforceOnlyActiveVisible(transitionTargetIndex);
                }
            }

            int fromIndex = currentActiveIndex;
            int toIndex = index;
            bool forward = toIndex > fromIndex;

            currentActiveIndex = toIndex;
            transitionTargetIndex = toIndex;
            model = model with { SelectedIndex = toIndex };
            RequestSaveModel();
            UpdateDots();

            var fromControl = GetOrCreateChildControl(fromIndex);
            var toControl = GetOrCreateChildControl(toIndex);

            if (toControl == null || fromControl == null || fromControl == toControl || WidgetsHost == null)
            {
                EnforceOnlyActiveVisible(toIndex);
                return;
            }

            double height = WidgetsHost.Bounds.Height;
            if (height <= 0) height = Bounds.Height;
            if (height <= 0) height = widgetLayoutProvider?.Get()?.Height ?? 0;
            if (height <= 0) height = 150;

            // Immediately ensure any third control is hidden and has no transforms
            for (int i = 0; i < model.Entries.Count; i++)
            {
                if (i != fromIndex && i != toIndex && childControls.TryGetValue(i, out var other) && other != null)
                {
                    other.IsVisible = false;
                    other.RenderTransform = null;
                }
            }

            var fromTransform = new TranslateTransform(0, 0);
            var toTransform = new TranslateTransform(0, forward ? height : -height);

            fromControl.RenderTransform = fromTransform;
            fromControl.IsVisible = true;
            fromControl.IsHitTestVisible = false;
            fromControl.Opacity = 1.0;
            fromControl.ZIndex = 0;

            toControl.RenderTransform = toTransform;
            toControl.IsVisible = true;
            toControl.IsHitTestVisible = false;
            toControl.Opacity = 1.0;
            toControl.ZIndex = 1;

            const double durationMs = 220.0;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(10)
            };
            activeTransitionTimer = timer;

            timer.Tick += (_, _) =>
            {
                try
                {
                    if (activeTransitionTimer != timer)
                    {
                        timer.Stop();
                        return;
                    }

                    double elapsed = stopwatch.Elapsed.TotalMilliseconds;
                    double progress = Math.Clamp(elapsed / durationMs, 0.0, 1.0);
                    // CubicEaseOut curve
                    double ease = 1.0 - Math.Pow(1.0 - progress, 3.0);

                    fromTransform.Y = forward ? -height * ease : height * ease;
                    toTransform.Y = forward ? height * (1.0 - ease) : -height * (1.0 - ease);

                    if (progress >= 1.0)
                    {
                        timer.Stop();
                        if (activeTransitionTimer == timer)
                        {
                            activeTransitionTimer = null;
                            transitionTargetIndex = -1;
                        }

                        // Offscreen now: hide first, do not clear transform so it never flashes at (0,0)
                        fromControl.IsVisible = false;
                        fromControl.Opacity = 1.0;

                        toControl.RenderTransform = null;
                        toControl.IsVisible = true;
                        toControl.IsHitTestVisible = true;
                        toControl.Opacity = 1.0;
                        toControl.ZIndex = 1;

                        EnforceOnlyActiveVisible(toIndex);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[WidgetStack] Transition tick error: {ex.Message}");
                    timer.Stop();
                    if (activeTransitionTimer == timer) activeTransitionTimer = null;
                    EnforceOnlyActiveVisible(toIndex);
                }
            };

            timer.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WidgetStack] SwitchToIndex error: {ex.Message}");
            EnforceOnlyActiveVisible(index);
        }
    }

    private void OnDotClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int index)
        {
            SwitchToIndex(index);
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        try
        {
            if (model?.Entries == null || !model.AllowWheelSwitch || model.Entries.Count <= 1) return;

            if (activeTransitionTimer != null)
            {
                e.Handled = true;
                return;
            }

            if (e.Delta.Y > 0)
            {
                // Up wheel -> previous widget
                var nextIndex = (model.SelectedIndex - 1 + model.Entries.Count) % model.Entries.Count;
                SwitchToIndex(nextIndex);
                e.Handled = true;
            }
            else if (e.Delta.Y < 0)
            {
                // Down wheel -> next widget
                var nextIndex = (model.SelectedIndex + 1) % model.Entries.Count;
                SwitchToIndex(nextIndex);
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WidgetStack] OnPointerWheelChanged error: {ex.Message}");
        }
    }

    public void UpdateChildSettings(int index, string? newSettingsJson, bool forceRecreateIfNotSelfRefreshing = false)
    {
        if (index < 0 || index >= model.Entries.Count) return;
        var entry = model.Entries[index];
        var updatedEntry = entry with { SettingsJson = newSettingsJson };
        var entries = model.Entries.ToList();
        entries[index] = updatedEntry;
        model = model with { Entries = entries };
        SaveModelDirect();

        if (childControls.TryGetValue(index, out var ctrl))
        {
            if (ctrl is IWidgetSelfRefreshing selfRefreshing)
            {
                var curLayout = widgetLayoutProvider?.Get();
                if (curLayout != null)
                {
                    var childLayout = new WidgetLayout(updatedEntry.AssemblyName, updatedEntry.ViewTypeName,
                        curLayout.X, curLayout.Y, curLayout.Width, curLayout.Height,
                        string.IsNullOrEmpty(newSettingsJson) ? null : JsonDocument.Parse(newSettingsJson).RootElement.Clone(),
                        curLayout.ContentScale);
                    selfRefreshing.Refresh(childLayout);
                }
            }
            else if (forceRecreateIfNotSelfRefreshing)
            {
                if (ctrl != null)
                {
                    WidgetsHost.Children.Remove(ctrl);
                }
                childControls.Remove(index);
                var newCtrl = GetOrCreateChildControl(index);
                if (newCtrl != null)
                {
                    bool isActive = (index == currentActiveIndex);
                    newCtrl.IsVisible = isActive;
                    newCtrl.IsHitTestVisible = isActive;
                    newCtrl.Opacity = 1.0;
                    newCtrl.ZIndex = isActive ? 1 : 0;
                    newCtrl.RenderTransform = null;
                }
            }
        }
    }

    public void UpdateFromSettings(WidgetStackModel newModel)
    {
        saveDebounceTimer?.Stop();
        WidgetsHost.Children.Clear();
        childControls.Clear();
        model = newModel;
        SaveModelDirect();
        RenderCurrentState();
    }

    private void RequestSaveModel()
    {
        if (saveDebounceTimer == null)
        {
            saveDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            saveDebounceTimer.Tick += (_, _) =>
            {
                saveDebounceTimer.Stop();
                SaveModelDirect();
            };
        }
        saveDebounceTimer.Stop();
        saveDebounceTimer.Start();
    }

    private void SaveModelDirect()
    {
        try
        {
            isSavingSelf = true;
            var curLayout = widgetLayoutProvider?.Get();
            if (curLayout == null) return;
            var json = JsonSerializer.SerializeToElement(model);
            widgetLayoutProvider?.Save(curLayout with { Settings = json });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WidgetStack] SaveModel error: {ex.Message}");
        }
        finally
        {
            isSavingSelf = false;
        }
    }

    public void Refresh(WidgetLayout layout)
    {
        if (isSavingSelf) return;

        var newModel = layout.GetModel<WidgetStackModel>();
        if (newModel == null) return;

        // If the stack entries are structurally equal (same widget views in same order)
        if (AreEntriesStructurallyEqual(model.Entries, newModel.Entries))
        {
            for (int i = 0; i < newModel.Entries.Count; i++)
            {
                var oldEntry = model.Entries[i];
                var newEntry = newModel.Entries[i];
                if (oldEntry.SettingsJson != newEntry.SettingsJson)
                {
                    if (childControls.TryGetValue(i, out var ctrl) && ctrl is IWidgetSelfRefreshing selfRefreshing)
                    {
                        var childLayout = new WidgetLayout(newEntry.AssemblyName, newEntry.ViewTypeName,
                            layout.X, layout.Y, layout.Width, layout.Height,
                            string.IsNullOrEmpty(newEntry.SettingsJson) ? null : JsonDocument.Parse(newEntry.SettingsJson).RootElement.Clone(),
                            layout.ContentScale);
                        selfRefreshing.Refresh(childLayout);
                    }
                    else
                    {
                        if (ctrl != null)
                        {
                            WidgetsHost.Children.Remove(ctrl);
                        }
                        childControls.Remove(i);
                        GetOrCreateChildControl(i);
                    }
                }
            }

            var oldSelectedIndex = model.SelectedIndex;
            model = newModel;
            UpdateDots();

            if (model.SelectedIndex != oldSelectedIndex)
            {
                SwitchToIndex(model.SelectedIndex);
            }
            return;
        }

        // Only recreate if widgets were added/removed/reordered
        model = newModel;
        WidgetsHost.Children.Clear();
        childControls.Clear();
        RenderCurrentState();
    }

    private static bool AreEntriesStructurallyEqual(List<StackedWidgetEntry>? a, List<StackedWidgetEntry>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;

        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].AssemblyName != b[i].AssemblyName ||
                a[i].ViewTypeName != b[i].ViewTypeName)
            {
                return false;
            }
        }
        return true;
    }
}

/// <summary>
/// Child layout provider that wraps the parent stack widget layout and routes settings
/// mutations back to the parent stack model.
/// </summary>
public class StackedChildLayoutProvider(Action<StackedWidgetEntry, string?> onSaveSettings, StackedWidgetEntry entry, IWidgetLayoutProvider parentLayoutProvider)
    : IWidgetLayoutProvider
{
    public string ScreenId
    {
        get => parentLayoutProvider.ScreenId;
        set => parentLayoutProvider.ScreenId = value;
    }

    public event DataChangedEvent<WidgetLayout>? DataChanging;
    public event DataChangedEvent<WidgetLayout>? DataChanged;

    public WidgetLayout Get()
    {
        var parent = parentLayoutProvider.Get();
        JsonElement? settings = null;
        if (!string.IsNullOrEmpty(entry.SettingsJson))
        {
            try { settings = JsonDocument.Parse(entry.SettingsJson).RootElement.Clone(); } catch { }
        }
        return new WidgetLayout(entry.AssemblyName, entry.ViewTypeName, parent.X, parent.Y, parent.Width, parent.Height, settings, parent.ContentScale);
    }

    public void Save(WidgetLayout data)
    {
        DataChanging?.Invoke(this, null, data);
        var settingsJson = data.Settings?.GetRawText();
        onSaveSettings(entry, settingsJson);
        DataChanged?.Invoke(this, null, data);
    }

    public void Remove()
    {
    }
}
