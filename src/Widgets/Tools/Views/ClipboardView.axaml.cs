using System;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Tools.Models;
using Tools.Services;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;
using uWidgets.Services;

namespace Tools.Views;

public partial class ClipboardView : UserControl, IWidgetSelfRefreshing
{
    private readonly ClipboardMonitorService monitor;
    private ClipboardModel model;
    private string currentFilter = "All";
    private readonly DispatcherTimer toastTimer;
    private WidgetTier currentTier = (WidgetTier)(-1);

    public ClipboardView() : this(new ClipboardModel()) { }

    public ClipboardView(ClipboardModel model)
    {
        this.model = model;
        monitor = ClipboardMonitorService.Instance;
        monitor.UpdateSettings(model);

        InitializeComponent();
        ApplyTier(WidgetTier.Small);

        toastTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1.5)
        };
        toastTimer.Tick += (_, _) =>
        {
            ToastBanner.IsVisible = false;
            toastTimer.Stop();
        };

        monitor.HistoryChanged += OnHistoryChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;

        RefreshDisplay();
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        RefreshDisplay();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        monitor.HistoryChanged -= OnHistoryChanged;
        SizeChanged -= OnSizeChanged;
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var size = e.NewSize;
        if (size.Width <= 0 || size.Height <= 0) return;

        var tier = ResolveTier(size);
        if (tier == currentTier) return;
        currentTier = tier;

        ApplyTier(tier);
    }

    private WidgetTier ResolveTier(Size size)
    {
        // 1. Physically narrow cards (<= 215 DIP) cannot fit medium/large layouts
        if (size.Width <= 215)
        {
            return WidgetTier.Small;
        }

        // 2. Try grid host span resolution
        try
        {
            var spanTier = SizeTiers.ResolveTier(this, size);
            if (spanTier != WidgetTier.Other)
            {
                return spanTier;
            }
        }
        catch { }

        // 3. Pixel fallback
        if (size.Width > 220 && size.Height > 220)
            return WidgetTier.Large;
        if (size.Width > 220 || size.Width > size.Height * 1.35)
            return WidgetTier.Medium;

        return WidgetTier.Small;
    }

    private void ApplyTier(WidgetTier tier)
    {
        bool isSmall = tier == WidgetTier.Small || tier == WidgetTier.Cell;

        Classes.Set("tier-small", isSmall);
        InlineFilterPanel.IsVisible = !isSmall;
        CompactFilterBtn.IsVisible = isSmall;
    }

    private void OnHistoryChanged()
    {
        Dispatcher.UIThread.Post(RefreshDisplay);
    }

    public void Refresh(WidgetLayout layout)
    {
        if (layout.Settings is not { } settings || settings.ValueKind != JsonValueKind.Object)
            return;

        try
        {
            var updated = settings.Deserialize<ClipboardModel>();
            if (updated != null)
            {
                model = updated;
                monitor.UpdateSettings(model);
                RefreshDisplay();
            }
        }
        catch { }
    }

    private void RefreshDisplay()
    {
        var items = monitor.History.AsEnumerable();

        if (currentFilter == "Text")
        {
            items = items.Where(i => i.Type == ClipboardType.Text);
        }
        else if (currentFilter == "Image")
        {
            items = items.Where(i => i.Type == ClipboardType.Image);
        }
        else if (currentFilter == "Files")
        {
            items = items.Where(i => i.Type == ClipboardType.Files);
        }

        var list = items.ToList();
        HistoryList.ItemsSource = list;

        CountBadgeText.Text = $"{monitor.History.Count}";
        EmptyStatePanel.IsVisible = list.Count == 0;
        HistoryScrollViewer.IsVisible = list.Count > 0;
    }

    private void OnFilterClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string filter)
        {
            SetFilter(filter);
        }
    }

    private void OnCompactFilterBtnClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.Open(btn);
        }
    }

    private void OnSelectFilterMenuItem(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Tag is string filter)
        {
            SetFilter(filter);
        }
    }

    private void SetFilter(string filter)
    {
        currentFilter = filter;

        FilterAllBtn.Classes.Set("active", filter == "All");
        FilterTextBtn.Classes.Set("active", filter == "Text");
        FilterImageBtn.Classes.Set("active", filter == "Image");
        FilterFilesBtn.Classes.Set("active", filter == "Files");

        CompactFilterText.Text = filter switch
        {
            "Text" => "文本",
            "Image" => "图片",
            "Files" => "文件",
            _ => "全部"
        };

        RefreshDisplay();
    }

    private void OnClearClicked(object? sender, RoutedEventArgs e)
    {
        monitor.ClearAll();
        ShowToast("已清空剪贴板历史");
    }

    private void OnCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!model.AutoCopyOnClick) return;
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed) return;

        if (sender is Border { DataContext: ClipboardItem item })
        {
            CopyItem(item);
        }
    }

    private void OnItemCopyClicked(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Button { Tag: ClipboardItem item })
        {
            CopyItem(item);
        }
    }

    private void OnItemDeleteClicked(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Button { Tag: ClipboardItem item })
        {
            monitor.RemoveItem(item);
        }
    }

    private void CopyItem(ClipboardItem item)
    {
        if (monitor.CopyToClipboard(item))
        {
            ShowToast("已复制到剪贴板");
        }
    }

    private void ShowToast(string message)
    {
        ToastText.Text = message;
        ToastBanner.IsVisible = true;
        toastTimer.Stop();
        toastTimer.Start();
    }
}
