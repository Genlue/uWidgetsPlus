using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;
using uWidgets.Core.Models.Settings;
using uWidgets.Locales;
using uWidgets.Services;
using uWidgets.Views;

namespace uWidgets.Views.Pages;

/// <summary>
/// Multi-screen management page: lists every attached screen with its matched
/// per-screen configuration (alias / grid / content scale / widget count) and
/// every saved configuration that is currently not connected.
/// <para>
/// Per screen: edit its grid (full-screen editor scoped to that screen), export
/// the screen as its own backup file, import a screen backup onto it, rename it
/// (alias), adjust its content scale, or manually bind a saved configuration to
/// it (manual rebinding for swapped / twin screens).
/// </para>
/// </summary>
public partial class MultiScreen : UserControl
{
    private readonly IAppSettingsProvider appSettingsProvider;
    private readonly ILayoutProvider layoutProvider;
    private readonly DisplayMonitorService displayMonitor;

    public MultiScreen(IAppSettingsProvider appSettingsProvider, ILayoutProvider layoutProvider, DisplayMonitorService displayMonitor)
    {
        this.appSettingsProvider = appSettingsProvider;
        this.layoutProvider = layoutProvider;
        this.displayMonitor = displayMonitor;
        InitializeComponent();
        Loaded += (_, _) => Reload();
        displayMonitor.ScreensChanged += OnScreensChanged;
        layoutProvider.DataChanged += OnLayoutChanged;
        appSettingsProvider.DataChanged += OnAppSettingsChanged;
        Unloaded += OnUnloaded;
    }

    private void OnScreensChanged(object? sender, EventArgs e) => Reload();
    private void OnLayoutChanged(object? sender, ScreensLayout? oldData, ScreensLayout newData) => Reload();
    private void OnAppSettingsChanged(object? sender, AppSettings? oldData, AppSettings newData) => Reload();

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        displayMonitor.ScreensChanged -= OnScreensChanged;
        layoutProvider.DataChanged -= OnLayoutChanged;
        appSettingsProvider.DataChanged -= OnAppSettingsChanged;
        Unloaded -= OnUnloaded;
    }

    private void Reload()
    {
        AttachedHeader.Text = Locale.Settings_MultiScreen_AttachedHeader;
        DetachedHeader.Text = Locale.Settings_MultiScreen_DetachedHeader;

        var attached = displayMonitor.Attached;
        var screens = layoutProvider.Get();
        var attachedIds = attached.Select(s => s.Config?.Id).Where(id => id != null).ToHashSet();

        AttachedList.Children.Clear();
        foreach (var screen in attached)
            AttachedList.Children.Add(BuildAttachedCard(screen));

        DetachedList.Children.Clear();
        foreach (var config in screens.Screens.Where(s => !attachedIds.Contains(s.Id)))
            DetachedList.Children.Add(BuildDetachedCard(config));

        DetachedHeader.IsVisible = DetachedList.Children.Count > 0;
        DetachedList.IsVisible = DetachedList.Children.Count > 0;
    }

    private Border BuildAttachedCard(AttachedScreen attached)
    {
        var screens = layoutProvider.Get();
        var config = attached.Config;
        var identity = attached.Identity;

        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14),
            Child = new StackPanel { Spacing = 10 }
        };
        var panel = (StackPanel) card.Child!;

        // 1. Status + Primary badge + Display name
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        header.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x2A, 0x2F, 0xC8, 0x40)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = Locale.Settings_MultiScreen_Connected,
                FontSize = 11,
                Foreground = Brushes.White
            }
        });
        if (identity.IsPrimary)
        {
            header.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x35, 0xE5, 0xA0, 0x0D)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "主屏幕",
                    FontSize = 11,
                    Foreground = Brushes.Gold
                }
            });
        }
        header.Children.Add(new TextBlock
        {
            Text = config?.DisplayName ?? (identity.FriendlyName.Length > 0 ? identity.FriendlyName : "Screen"),
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        panel.Children.Add(header);

        // 2. Hardware info & widget count
        var infoPanel = new StackPanel { Spacing = 2 };
        infoPanel.Children.Add(new TextBlock
        {
            Text = $"{identity.FriendlyName} · {identity.Width}×{identity.Height} · {(int) Math.Round(identity.Scaling * 100)}% DPI",
            FontSize = 12,
            Opacity = 0.65
        });
        infoPanel.Children.Add(new TextBlock
        {
            Text = string.Format(Locale.Settings_MultiScreen_WidgetCount, config?.Layout.Count ?? 0),
            FontSize = 12,
            Opacity = 0.65
        });
        panel.Children.Add(infoPanel);

        // 3. Screen alias
        var aliasRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        aliasRow.Children.Add(new TextBlock { Text = "屏幕别名", FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.8 });
        var alias = new TextBox
        {
            Text = config?.Alias ?? "",
            Watermark = Locale.Settings_MultiScreen_AliasPlaceholder,
            FontSize = 12,
            MinWidth = 240
        };
        alias.LostFocus += (_, _) => SaveConfig(config ?? displayMonitor.EnsureConfig(attached), entry => entry with { Alias = string.IsNullOrWhiteSpace(alias.Text) ? null : alias.Text.Trim() });
        aliasRow.Children.Add(alias);
        panel.Children.Add(aliasRow);

        panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)), Margin = new Thickness(0, 2) });

        // 4. Per-Screen Grid Section (本屏独立网格)
        var gridSection = new StackPanel { Spacing = 6 };
        var hasCustomGrid = config?.Grid != null;
        var globalGrid = appSettingsProvider.Get().Grid ?? uWidgets.Core.Models.Settings.Grid.Default;
        var effectiveGrid = config?.Grid ?? globalGrid;

        gridSection.Children.Add(new TextBlock
        {
            Text = "📐 桌面网格 (本屏独立)",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold
        });

        gridSection.Children.Add(new TextBlock
        {
            Text = hasCustomGrid
                ? $"当前为本屏独立网格 · {effectiveGrid.Columns}列 × {effectiveGrid.Rows}行 · 格子 {effectiveGrid.CellPercent:0.##}%"
                : $"当前跟随全局默认 · {globalGrid.Columns}列 × {globalGrid.Rows}行 (在此修改将转为本屏专属网格)",
            FontSize = 11,
            Opacity = hasCustomGrid ? 0.9 : 0.6
        });

        // Grid parameters: Columns, Rows, CellSize
        var gridInputs = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2) };

        var colsBox = new NumericUpDown
        {
            Minimum = 1, Maximum = 60, Increment = 1, FormatString = "F0",
            Value = effectiveGrid.Columns, MinWidth = 100, Margin = new Thickness(0, 0, 10, 4)
        };
        colsBox.LostFocus += (_, _) =>
        {
            int cols = (int) Math.Clamp(colsBox.Value ?? effectiveGrid.Columns, 1, 60);
            var cur = config?.Grid ?? globalGrid;
            if (cur.Columns == cols && config?.Grid != null) return;
            var nextGrid = cur with { Columns = cols };
            SaveConfig(config ?? displayMonitor.EnsureConfig(attached), entry => entry with { Grid = nextGrid });
            Reload();
        };
        gridInputs.Children.Add(new StackPanel { Spacing = 2, Children = { new TextBlock { Text = "列数 (Columns)", FontSize = 11, Opacity = 0.7 }, colsBox } });

        var rowsBox = new NumericUpDown
        {
            Minimum = 1, Maximum = 40, Increment = 1, FormatString = "F0",
            Value = effectiveGrid.Rows, MinWidth = 100, Margin = new Thickness(0, 0, 10, 4)
        };
        rowsBox.LostFocus += (_, _) =>
        {
            int rows = (int) Math.Clamp(rowsBox.Value ?? effectiveGrid.Rows, 1, 40);
            var cur = config?.Grid ?? globalGrid;
            if (cur.Rows == rows && config?.Grid != null) return;
            var nextGrid = cur with { Rows = rows };
            SaveConfig(config ?? displayMonitor.EnsureConfig(attached), entry => entry with { Grid = nextGrid });
            Reload();
        };
        gridInputs.Children.Add(new StackPanel { Spacing = 2, Children = { new TextBlock { Text = "行数 (Rows)", FontSize = 11, Opacity = 0.7 }, rowsBox } });

        var cellBox = new NumericUpDown
        {
            Minimum = 0.1m, Maximum = 50m, Increment = 0.1m, FormatString = "0.##",
            Value = (decimal) effectiveGrid.CellPercent, MinWidth = 110, Margin = new Thickness(0, 0, 10, 4)
        };
        cellBox.LostFocus += (_, _) =>
        {
            double cell = Math.Round((double) (cellBox.Value ?? (decimal) effectiveGrid.CellPercent), 2);
            var cur = config?.Grid ?? globalGrid;
            if (Math.Abs(cur.CellPercent - cell) < 0.001 && config?.Grid != null) return;
            var nextGrid = cur with { CellPercent = cell };
            SaveConfig(config ?? displayMonitor.EnsureConfig(attached), entry => entry with { Grid = nextGrid });
            Reload();
        };
        gridInputs.Children.Add(new StackPanel { Spacing = 2, Children = { new TextBlock { Text = "格子宽度 (%)", FontSize = 11, Opacity = 0.7 }, cellBox } });

        gridSection.Children.Add(gridInputs);

        var gridButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 2) };
        gridButtons.Children.Add(Button("✏️ 全屏可视化网格编辑", (_, _) =>
        {
            var targetConfig = config ?? displayMonitor.EnsureConfig(attached);
            new GridEditor(appSettingsProvider, layoutProvider, displayMonitor, targetConfig.Id, attached.Screen).Show();
        }));
        if (hasCustomGrid)
        {
            gridButtons.Children.Add(Button(Locale.Settings_MultiScreen_ResetGrid ?? "重置为全局默认", (_, _) =>
            {
                SaveConfig(config!, entry => entry with { Grid = null });
                Reload();
            }));
        }
        gridSection.Children.Add(gridButtons);
        panel.Children.Add(gridSection);

        panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)), Margin = new Thickness(0, 2) });

        // 5. Per-Screen Margin & Content Scale Section (边距与缩放)
        var dimsSection = new StackPanel { Spacing = 6 };
        dimsSection.Children.Add(new TextBlock
        {
            Text = "📏 边距与缩放 (本屏独立)",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold
        });

        var dimsRow = new WrapPanel { Orientation = Orientation.Horizontal };

        // Margin
        var globalMargin = appSettingsProvider.Get().Dimensions.Margin;
        var hasCustomMargin = config?.Margin != null;
        var marginBox = new NumericUpDown
        {
            Minimum = 0, Maximum = 48, Increment = 1, FormatString = "F0",
            Value = (decimal)(config?.Margin ?? globalMargin),
            MinWidth = 110, Margin = new Thickness(0, 0, 10, 4)
        };
        marginBox.LostFocus += (_, _) =>
        {
            var val = (double)Math.Clamp(marginBox.Value ?? (decimal)globalMargin, 0m, 48m);
            if (config?.Margin == val) return;
            SaveConfig(config ?? displayMonitor.EnsureConfig(attached), entry => entry with { Margin = val });
            Reload();
        };

        var marginContainer = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = hasCustomMargin ? "组件内边距 Margin (专属)" : $"组件内边距 Margin (默认 {globalMargin:0}px)",
                    FontSize = 11,
                    Opacity = hasCustomMargin ? 0.95 : 0.7
                },
                marginBox
            }
        };
        dimsRow.Children.Add(marginContainer);

        // Content Scale
        var scaleInput = new NumericUpDown
        {
            Minimum = 0.2m, Maximum = 3.0m, Increment = 0.05m, FormatString = "0.00",
            Value = (decimal)(config?.ContentScale ?? 1.0),
            MinWidth = 110, Margin = new Thickness(0, 0, 10, 4)
        };
        scaleInput.LostFocus += (_, _) =>
        {
            var value = (double)Math.Clamp(scaleInput.Value ?? 1.0m, 0.2m, 3.0m);
            if (config?.ContentScale is { } current && Math.Abs(current - value) < 0.001) return;
            SaveConfig(config ?? displayMonitor.EnsureConfig(attached), entry => entry with { ContentScale = value });
        };
        dimsRow.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock { Text = Locale.Settings_MultiScreen_ContentScale ?? "内容整体缩放", FontSize = 11, Opacity = 0.7 },
                scaleInput
            }
        });

        if (hasCustomMargin)
        {
            var resetMarginBtn = Button("恢复默认边距", (_, _) =>
            {
                SaveConfig(config!, entry => entry with { Margin = null });
                Reload();
            });
            resetMarginBtn.VerticalAlignment = VerticalAlignment.Bottom;
            resetMarginBtn.Margin = new Thickness(0, 0, 0, 4);
            dimsRow.Children.Add(resetMarginBtn);
        }

        dimsSection.Children.Add(dimsRow);
        panel.Children.Add(dimsSection);

        panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)), Margin = new Thickness(0, 2) });

        // 6. Maintenance & Rebinding
        var maintSection = new StackPanel { Spacing = 6 };
        maintSection.Children.Add(new TextBlock { Text = "⚙️ 布局维护与端口绑定", FontSize = 13, FontWeight = FontWeight.SemiBold });

        var maintButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        maintButtons.Children.Add(Button(Locale.Settings_MultiScreen_Export, async (_, _) => await ExportScreen(config ?? displayMonitor.EnsureConfig(attached))));
        maintButtons.Children.Add(Button(Locale.Settings_MultiScreen_Import, async (_, _) => await ImportScreen(attached)));
        maintSection.Children.Add(maintButtons);

        // Manual rebinding
        var stored = screens.Screens.ToList();
        var bindItems = new List<object> { Locale.Settings_MultiScreen_Rebind };
        foreach (var entry in stored)
            bindItems.Add(entry);
        var bindCombo = new ComboBox
        {
            ItemsSource = bindItems,
            SelectedIndex = config != null ? bindItems.IndexOf(config) : 0,
            MinWidth = 200
        };
        bindCombo.SelectionChanged += (_, _) => Rebind(attached, bindCombo, config);
        maintSection.Children.Add(new TextBlock { Text = Locale.Settings_MultiScreen_RebindHint, FontSize = 11, Opacity = 0.55 });
        maintSection.Children.Add(bindCombo);

        panel.Children.Add(maintSection);

        return card;
    }

    private Border BuildDetachedCard(ScreenLayout config)
    {
        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x0D, 0xFF, 0xFF, 0xFF)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14),
            Child = new StackPanel { Spacing = 8 }
        };
        var panel = (StackPanel) card.Child!;

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        header.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x2A, 0x88, 0x88, 0x88)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = Locale.Settings_MultiScreen_NotConnected, FontSize = 11, Foreground = Brushes.White }
        });
        header.Children.Add(new TextBlock
        {
            Text = config.DisplayName,
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        panel.Children.Add(header);

        panel.Children.Add(new TextBlock
        {
            Text = $"{config.Key?.Split('|')[0] ?? "Primary"} · {string.Format(Locale.Settings_MultiScreen_WidgetCount, config.Layout.Count)}",
            FontSize = 12,
            Opacity = 0.6
        });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(Button(Locale.Settings_MultiScreen_Export, async (_, _) => await ExportScreen(config)));
        buttons.Children.Add(Button(Locale.Settings_MultiScreen_Delete, async (_, _) => await DeleteConfig(config)));
        panel.Children.Add(buttons);

        return card;
    }

    private static Button Button(string text, EventHandler<RoutedEventArgs> handler)
    {
        var btn = new Button
        {
            Content = text,
            FontSize = 12,
            Padding = new Thickness(12, 6)
        };
        btn.Click += handler;
        return btn;
    }

    private void SaveConfig(ScreenLayout entry, Func<ScreenLayout, ScreenLayout> update)
    {
        var screens = layoutProvider.Get();
        layoutProvider.Save(screens.UpsertScreen(update(entry)));
        displayMonitor.Refresh();
    }

    private void Rebind(AttachedScreen attached, ComboBox combo, ScreenLayout? current)
    {
        if (combo.SelectedItem == null) return;
        if (ReferenceEquals(combo.SelectedItem, current)) return;
        if (combo.SelectedItem is not ScreenLayout target) return; // placeholder row

        var screens = layoutProvider.Get();
        var deviceName = attached.Identity.DeviceName;

        // Unbind any other configuration pinned to this device.
        screens = screens with
        {
            Screens = screens.Screens.Select(s => s.DeviceName == deviceName ? s with { DeviceName = null } : s).ToList()
        };

        var entry = screens.FindById(target.Id);
        if (entry != null)
            screens = screens.WithScreen(entry with { DeviceName = deviceName });

        layoutProvider.Save(screens);
        displayMonitor.Refresh();
        Reload();
    }

    private async Task ExportScreen(ScreenLayout config)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        var storage = owner?.StorageProvider;
        if (owner == null || storage == null) return;

        try
        {
            var safeName = string.Join("-", (config.DisplayName ?? "screen").Split(Path.GetInvalidFileNameChars()));
            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "uWidgets",
                SuggestedFileName = $"uWidgets-screen-{safeName}.json",
                DefaultExtension = "json",
                FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
            });
            if (file == null) return;

            await using var stream = await file.OpenWriteAsync();
            await BackupService.WriteAsync(stream, BackupService.CreateSingle(appSettingsProvider, config));
        }
        catch (Exception ex)
        {
            await ConfirmDialog.InformAsync(owner,
                string.Format(Locale.Settings_Advanced_Backup_Error, ex.Message),
                Locale.Settings_Advanced_Backup_OkButton);
        }
    }

    private async Task ImportScreen(AttachedScreen attached)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        var storage = owner?.StorageProvider;
        if (owner == null || storage == null) return;

        try
        {
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "uWidgets",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
            });
            if (files.Count == 0) return;

            var file = files[0];
            await using var readStream = await file.OpenReadAsync();
            var backup = await BackupService.ReadAsync(readStream);

            var imported = backup.Screens.Screens.First();
            var confirmed = await ConfirmDialog.ConfirmAsync(owner,
                string.Format(Locale.Settings_MultiScreen_ImportConfirm, imported.DisplayName),
                Locale.Settings_Advanced_Backup_ImportButton,
                Locale.Settings_Advanced_Backup_CancelButton);
            if (!confirmed) return;

            ApplyImportTo(attached, imported);
        }
        catch (FormatException)
        {
            await ConfirmDialog.InformAsync(owner, Locale.Settings_Advanced_Backup_InvalidFile, Locale.Settings_Advanced_Backup_OkButton);
        }
        catch (Exception ex)
        {
            await ConfirmDialog.InformAsync(owner,
                string.Format(Locale.Settings_Advanced_Backup_Error, ex.Message),
                Locale.Settings_Advanced_Backup_OkButton);
        }
    }

    /// <summary>
    /// Import the configuration of a screen backup onto the given attached screen: the
    /// imported layout is position-scaled when the resolutions differ, everything is
    /// stored under THIS screen's identity (so it stays matched locally), then the app
    /// restarts to rebuild the widgets in place.
    /// </summary>
    private void ApplyImportTo(AttachedScreen attached, ScreenLayout imported)
    {
        var screens = layoutProvider.Get();

        // Legacy backup (Key == null): its stored positions are ABSOLUTE desktop
        // pixels. Copying them raw into a per-screen entry (which stores
        // RELATIVE coordinates) is exactly what produced the ghost 1×1 widget at
        // the top-left / stacked double widgets after an import. Redistribute
        // every widget to the attached screen that physically contains it — the
        // same rule the startup migration uses.
        if (imported.Key == null)
        {
            foreach (var item in imported.Layout)
            {
                var center = new PixelPoint(
                    item.X + item.Width / 2,
                    item.Y + item.Height / 2);
                var owner = displayMonitor.Attached.FirstOrDefault(a =>
                    a.Screen.WorkingArea.Contains(center));
                if (owner == null) continue; // outside every screen → cannot place

                var config = owner.Config ?? displayMonitor.EnsureConfig(owner);
                var entry = owner.Config?.Id == ScreensLayout.LegacyPrimaryId
                    ? item // legacy primary entry keeps absolute coordinates
                    : item with
                    {
                        X = item.X - owner.Screen.WorkingArea.X,
                        Y = item.Y - owner.Screen.WorkingArea.Y
                    };
                screens = screens.UpsertScreen(config with { Layout = [.. config.Layout, entry] });
            }

            layoutProvider.Save(screens);
            AppRestart.Restart();
            return;
        }

        // Per-screen backup: scale positions from the source resolution onto the
        // target screen's working area and rebind the entry to this screen.
        var target = attached.Config ?? displayMonitor.EnsureConfig(attached);
        var attachedArea = attached.Screen.WorkingArea;

        var importedSize = imported.Key?.Split('|')[1] ?? "";
        var parts = importedSize.Split('x');
        var scaleX = 1.0;
        var scaleY = 1.0;
        if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h) && w > 0 && h > 0)
        {
            scaleX = attachedArea.Width / (double) w;
            scaleY = attachedArea.Height / (double) h;
        }

        var layout = imported.Layout.Select(item => item with
        {
            X = (int) Math.Round(item.X * scaleX),
            Y = (int) Math.Round(item.Y * scaleY),
            Width = (int) Math.Round(item.Width * scaleX),
            Height = (int) Math.Round(item.Height * scaleY)
        }).ToList();

        var updated = target with
        {
            Key = attached.Identity.Key,
            Grid = imported.Grid ?? target.Grid,
            ContentScale = imported.ContentScale ?? target.ContentScale,
            Alias = imported.Alias ?? target.Alias,
            Layout = layout
        };
        layoutProvider.Save(screens.UpsertScreen(updated));
        AppRestart.Restart();
    }

    private async Task DeleteConfig(ScreenLayout config)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null) return;

        var confirmed = await ConfirmDialog.ConfirmAsync(owner,
            $"Delete the configuration \"{config.DisplayName}\" and its {config.Layout.Count} widgets?",
            Locale.Settings_Advanced_Backup_OkButton,
            Locale.Settings_Advanced_Backup_CancelButton);
        if (!confirmed) return;

        var screens = layoutProvider.Get();
        layoutProvider.Save(screens with { Screens = screens.Screens.Where(s => s.Id != config.Id).ToList() });
        displayMonitor.Refresh();
        Reload();
    }
}