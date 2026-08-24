using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using uWidgets.Core.Interfaces;
using uWidgets.Locales;
using uWidgets.Services;
using uWidgets.ViewModels;

namespace uWidgets.Views.Pages;

public partial class Advanced : UserControl
{
    private readonly IAppSettingsProvider appSettingsProvider;
    private readonly ILayoutProvider layoutProvider;

    public Advanced(IAppSettingsProvider appSettingsProvider, ILayoutProvider layoutProvider)
    {
        this.appSettingsProvider = appSettingsProvider;
        this.layoutProvider = layoutProvider;
        DataContext = new AdvancedViewModel(appSettingsProvider);
        InitializeComponent();
    }

    private void OnEditGridClicked(object? sender, RoutedEventArgs e)
    {
        new GridEditor(appSettingsProvider).Show();
    }

    /// <summary>
    /// Export the whole app state (app settings incl. the manual grid config,
    /// and the widget layout incl. per-card content) to a JSON backup file.
    /// </summary>
    private async void OnExportBackup(object? sender, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        var storage = owner?.StorageProvider;
        if (owner == null || storage == null) return;

        try
        {
            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "uWidgets",
                SuggestedFileName = "uWidgets-backup.json",
                DefaultExtension = "json",
                FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
            });
            if (file == null) return;

            await using var stream = await file.OpenWriteAsync();
            await BackupService.WriteAsync(stream, BackupService.Create(appSettingsProvider, layoutProvider));
        }
        catch (Exception ex)
        {
            await ConfirmDialog.InformAsync(owner,
                string.Format(Locale.Settings_Advanced_Backup_Error, ex.Message),
                Locale.Settings_Advanced_Backup_OkButton);
        }
    }

    /// <summary>
    /// Import a backup file: validate it, confirm with the user, restore both
    /// files through the providers and restart the app so the widget windows
    /// are rebuilt from the restored layout.
    /// </summary>
    private async void OnImportBackup(object? sender, RoutedEventArgs e)
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
            await using var stream = await file.OpenReadAsync();
            var backup = await BackupService.ReadAsync(stream);

            var confirmed = await ConfirmDialog.ConfirmAsync(owner,
                Locale.Settings_Advanced_BackupImport_Confirm,
                Locale.Settings_Advanced_Backup_ImportButton,
                Locale.Settings_Advanced_Backup_CancelButton);
            if (!confirmed) return;

            // Restore through the providers so the in-memory cache and the live
            // listeners stay in sync, then restart to rebuild the widgets.
            appSettingsProvider.Save(backup.AppSettings);
            layoutProvider.Save(backup.Layout);
            AppRestart.Restart();
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
}