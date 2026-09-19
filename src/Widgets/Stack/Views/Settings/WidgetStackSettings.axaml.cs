using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using StackWidgets.Models;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;
using uWidgets.Core.Models.Attributes;
using uWidgets.Views;

namespace StackWidgets.Views.Settings;

public record AvailableWidgetOption(string AssemblyName, string ViewTypeName, string Title)
{
    public override string ToString() => Title;
}

public partial class WidgetStackSettings : UserControl
{
    private readonly IWidgetLayoutProvider widgetLayoutProvider;
    private readonly IAssemblyProvider assemblyProvider;
    private WidgetStackModel model;
    private bool isInitializing = true;

    private static readonly List<AvailableWidgetOption> AvailableOptions =
    [
        new("Clock", "AnalogI", "时钟 (经典表盘 I)"),
        new("Clock", "AnalogII", "时钟 (经典表盘 II)"),
        new("Clock", "Digital", "时钟 (数字时钟)"),
        new("Notes", "Note", "便签"),
        new("Reminders", "List", "待办清单"),
        new("Monitor", "SingleMetric", "系统监控 (轻量仪表)"),
        new("Monitor", "MultiDashboard", "系统监控 (全能看板)"),
        new("Weather", "Forecast", "天气预报"),
        new("Batteries", "BatteriesView", "电池电量"),
        new("Pomodoro", "PomodoroView", "番茄工作法"),
        new("Progress", "ProgressView", "进度刻度"),
        new("Search", "SearchView", "快捷搜索")
    ];

    public WidgetStackSettings() : this(null!, null!)
    {
    }

    public WidgetStackSettings(IWidgetLayoutProvider? widgetLayoutProvider, IAssemblyProvider? assemblyProvider = null)
    {
        this.widgetLayoutProvider = widgetLayoutProvider!;
        this.assemblyProvider = assemblyProvider ?? (uWidgets.App.Services?.GetService(typeof(IAssemblyProvider)) as IAssemblyProvider)!;
        model = widgetLayoutProvider?.Get().GetModel<WidgetStackModel>() ?? new WidgetStackModel();

        InitializeComponent();

        AvailableWidgetsCombo.ItemsSource = AvailableOptions;
        AvailableWidgetsCombo.SelectedIndex = 0;

        LoadFromModel();
        isInitializing = false;
    }

    private void LoadFromModel()
    {
        AllowWheelSwitch.IsChecked = model.AllowWheelSwitch;
        RefreshList();
    }

    private bool CheckHasSettings(StackedWidgetEntry entry)
    {
        try
        {
            var assembly = assemblyProvider?.LoadAssembly(entry.AssemblyName);
            var widgetInfo = assembly?
                .GetCustomAttributes<WidgetInfoAttribute>()
                .FirstOrDefault(a => a.ViewType.Name == entry.ViewTypeName);
            return widgetInfo?.EditModelViewType != null;
        }
        catch
        {
            return false;
        }
    }

    private void RefreshList()
    {
        var items = new List<StackedWidgetSettingItem>();
        for (int i = 0; i < model.Entries.Count; i++)
        {
            var entry = model.Entries[i];
            items.Add(new StackedWidgetSettingItem
            {
                Entry = entry,
                Index = i,
                HasSettings = CheckHasSettings(entry)
            });
        }
        ItemsList.ItemsSource = null;
        ItemsList.ItemsSource = items;
    }

    private void OnToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (isInitializing) return;
        model = model with { AllowWheelSwitch = AllowWheelSwitch.IsChecked == true };
        SaveModel();
    }

    private void OnAddWidgetClicked(object? sender, RoutedEventArgs e)
    {
        if (AvailableWidgetsCombo.SelectedItem is AvailableWidgetOption opt)
        {
            var newEntry = new StackedWidgetEntry(opt.AssemblyName, opt.ViewTypeName, opt.Title);
            var list = model.Entries.ToList();
            list.Add(newEntry);
            model = model with { Entries = list };
            SaveModel();
            RefreshList();
        }
    }

    private async void OnEditChildClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: StackedWidgetSettingItem item }) return;

        var entry = item.Entry;
        var assembly = assemblyProvider?.LoadAssembly(entry.AssemblyName);
        var widgetInfo = assembly?
            .GetCustomAttributes<WidgetInfoAttribute>()
            .FirstOrDefault(a => a.ViewType.Name == entry.ViewTypeName);

        if (widgetInfo?.EditModelViewType == null) return;

        var childLayoutProvider = new StackedChildLayoutProvider((updatedEntry, json) =>
        {
            var list = model.Entries.ToList();
            if (item.Index >= 0 && item.Index < list.Count)
            {
                list[item.Index] = list[item.Index] with { SettingsJson = json };
                model = model with { Entries = list };
                SaveModel();
            }
        }, entry, widgetLayoutProvider);

        var editControl = (UserControl)assemblyProvider!.Activate(widgetInfo.EditModelViewType, childLayoutProvider);
        var editWindow = new EditWidget(childLayoutProvider, editControl);

        var topLevel = TopLevel.GetTopLevel(this) as Window;
        if (topLevel != null)
        {
            await editWindow.ShowDialog(topLevel);
        }
        else
        {
            editWindow.Show();
        }
        RefreshList();
    }

    private void OnMoveUpClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is StackedWidgetSettingItem item)
        {
            int idx = item.Index;
            if (idx > 0)
            {
                var list = model.Entries.ToList();
                var entry = list[idx];
                list.RemoveAt(idx);
                list.Insert(idx - 1, entry);
                model = model with { Entries = list, SelectedIndex = Math.Min(model.SelectedIndex, list.Count - 1) };
                SaveModel();
                RefreshList();
            }
        }
    }

    private void OnMoveDownClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is StackedWidgetSettingItem item)
        {
            int idx = item.Index;
            var list = model.Entries.ToList();
            if (idx >= 0 && idx < list.Count - 1)
            {
                var entry = list[idx];
                list.RemoveAt(idx);
                list.Insert(idx + 1, entry);
                model = model with { Entries = list, SelectedIndex = Math.Min(model.SelectedIndex, list.Count - 1) };
                SaveModel();
                RefreshList();
            }
        }
    }

    private void OnRemoveClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is StackedWidgetSettingItem item)
        {
            var list = model.Entries.ToList();
            if (list.Count <= 1) return; // Keep at least one widget

            int idx = item.Index;
            if (idx >= 0 && idx < list.Count)
            {
                list.RemoveAt(idx);
                int newSelected = model.SelectedIndex;
                if (newSelected >= list.Count) newSelected = list.Count - 1;
                model = model with { Entries = list, SelectedIndex = newSelected };
                SaveModel();
                RefreshList();
            }
        }
    }

    private void SaveModel()
    {
        try
        {
            var curLayout = widgetLayoutProvider.Get();
            var json = JsonSerializer.SerializeToElement(model);
            widgetLayoutProvider.Save(curLayout with { Settings = json });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WidgetStackSettings] SaveModel error: {ex.Message}");
        }
    }
}
