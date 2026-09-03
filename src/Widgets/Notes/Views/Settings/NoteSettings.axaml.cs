using System.IO;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Notes.Locales;
using Notes.Models;
using uWidgets.Core.Interfaces;

namespace Notes.Views.Settings;

/// <summary>
/// Per-widget note settings: markdown rendering, the colored title bar
/// (follow accent / custom color, plus opacity) and the body source
/// (internal text, one markdown file, or a folder of markdown files with
/// a document count and recent/manual selection).
/// </summary>
public partial class NoteSettings : UserControl
{
    private readonly IWidgetLayoutProvider widgetLayoutProvider;

    public NoteSettings(IWidgetLayoutProvider widgetLayoutProvider)
    {
        this.widgetLayoutProvider = widgetLayoutProvider;
        InitializeComponent();

        HeaderModeBox.ItemsSource = new[] { Locale.Notes_Header_FollowAccent, Locale.Notes_Header_Custom };
        SourceBox.ItemsSource = new[] { Locale.Notes_Source_Internal, Locale.Notes_Source_File, Locale.Notes_Source_Folder };
        OrderBox.ItemsSource = new[] { Locale.Notes_Order_Recent, Locale.Notes_Order_Manual };

        // Programmatic pre-fill happens BEFORE the handlers are attached, so the
        // handlers only ever see user interaction.
        Load(widgetLayoutProvider.Get().GetModel<NoteModel>() ?? new NoteModel(Locale.Notes_Title));

        MarkdownToggle.Click += (_, _) =>
            UpdateModel(m => m with { Markdown = MarkdownToggle.IsChecked == true });
        BodyPaddingBox.ValueChanged += (_, _) =>
            UpdateModel(m => m with { BodyPadding = (int)(BodyPaddingBox.Value ?? 4) });
        HeaderModeBox.SelectionChanged += (_, _) =>
        {
            var follow = HeaderModeBox.SelectedIndex == 0;
            UpdateModel(m => m with
            {
                FollowAccentHeader = follow,
                HeaderColor = follow ? m.HeaderColor : m.HeaderColor ?? "#FF3376CD",
            });
            HeaderColorPicker.IsVisible = !follow;
        };
        HeaderColorPicker.ColorChanged += (_, _) =>
            UpdateModel(m => m with { HeaderColor = HeaderColorPicker.Color.ToString() });
        HeaderOpacityBox.ValueChanged += (_, _) =>
            UpdateModel(m => m with { HeaderOpacity = (double)(HeaderOpacityBox.Value ?? 0.15m) });
        SourceBox.SelectionChanged += (_, _) =>
        {
            UpdateModel(m => m with { Source = (NoteSource)Math.Max(0, SourceBox.SelectedIndex) });
            UpdateSourceVisibility();
            RebuildFileList();
        };
        DocumentCountBox.ValueChanged += (_, _) =>
            UpdateModel(m => m with { DocumentCount = (int)(DocumentCountBox.Value ?? 3) });
        OrderBox.SelectionChanged += (_, _) =>
        {
            UpdateModel(m => m with { RecentFiles = OrderBox.SelectedIndex == 0 });
            FilesSetting.IsVisible = OrderBox.SelectedIndex == 1;
        };
    }

    private NoteModel CurrentModel =>
        widgetLayoutProvider.Get().GetModel<NoteModel>() ?? new NoteModel(Locale.Notes_Title);

    private void Load(NoteModel model)
    {
        MarkdownToggle.IsChecked = model.Markdown;
        BodyPaddingBox.Value = Math.Clamp(model.BodyPadding, 0, 64);
        HeaderModeBox.SelectedIndex = model.FollowAccentHeader ? 0 : 1;
        HeaderColorPicker.IsVisible = !model.FollowAccentHeader;
        HeaderColorPicker.Color = Color.TryParse(model.HeaderColor, out var color) ? color : Color.Parse("#3376CD");
        HeaderOpacityBox.Value = (decimal)Math.Round(model.HeaderOpacity, 2);
        SourceBox.SelectedIndex = (int)model.Source;
        OrderBox.SelectedIndex = model.RecentFiles ? 0 : 1;
        DocumentCountBox.Value = Math.Clamp(model.DocumentCount, 1, 8);
        FilesSetting.IsVisible = model.Source == NoteSource.Folder && !model.RecentFiles;
        UpdateSourceVisibility();
        RebuildFileList();
    }

    private void UpdateSourceVisibility()
    {
        var source = (NoteSource)Math.Max(0, SourceBox.SelectedIndex);
        BrowseButton.IsVisible = source != NoteSource.Internal;
        var path = CurrentModel.Path;
        PathText.IsVisible = source != NoteSource.Internal && !string.IsNullOrEmpty(path);
        PathText.Text = path ?? "";
        FolderSection.IsVisible = source == NoteSource.Folder;
    }

    private async void Browse(object? sender, RoutedEventArgs e)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var source = (NoteSource)Math.Max(0, SourceBox.SelectedIndex);
        string? picked = null;

        if (source == NoteSource.Folder)
        {
            var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = Locale.Notes_Source_Folder,
                AllowMultiple = false,
            });
            picked = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        }
        else if (source == NoteSource.File)
        {
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Locale.Notes_Source_File,
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("Markdown") { Patterns = ["*.md"] } },
            });
            picked = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        }

        if (picked == null) return;
        UpdateModel(m => m with { Path = picked });
        UpdateSourceVisibility();
        RebuildFileList();
    }

    private void RebuildFileList()
    {
        FilesPanel.Children.Clear();
        var model = CurrentModel;
        if (model.Source != NoteSource.Folder || model.Path == null || !Directory.Exists(model.Path)) return;

        string[] files;
        try
        {
            files = Directory.GetFiles(model.Path, "*.md", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            return;
        }

        Array.Sort(files, (a, b) => string.Compare(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase));
        var selected = new HashSet<string>(model.SelectedFiles ?? [], StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            var check = new CheckBox
            {
                Content = name,
                IsChecked = selected.Contains(name),
                FontSize = 12,
            };
            void OnCheckChanged(object? sender, RoutedEventArgs e)
            {
                if (check.IsChecked == true) selected.Add(name);
                else selected.Remove(name);
                UpdateModel(m => m with { SelectedFiles = selected.ToList() });
            }
            check.Checked += OnCheckChanged;
            check.Unchecked += OnCheckChanged;
            FilesPanel.Children.Add(check);
        }
    }

    private void UpdateModel(Func<NoteModel, NoteModel> mutate)
    {
        var layout = widgetLayoutProvider.Get();
        var model = layout.GetModel<NoteModel>() ?? new NoteModel(Locale.Notes_Title);
        layout = layout with { Settings = JsonSerializer.SerializeToElement(mutate(model)) };
        widgetLayoutProvider.Save(layout);
    }
}
