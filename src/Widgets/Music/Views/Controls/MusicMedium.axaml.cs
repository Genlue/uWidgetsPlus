using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Music.Models;
using Music.ViewModels;

namespace Music.Views.Controls;

public partial class MusicMedium : UserControl
{
    private readonly MusicViewModel viewModel;
    public MusicMedium() : this(new MusicViewModel(new MusicModel())) { }

    public MusicMedium(MusicViewModel viewModel)
    {
        this.viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        SizeChanged += OnSizeChanged;
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var h = e.NewSize.Height;
        var w = e.NewSize.Width;
        if (h <= 0 || w <= 0) return;

        var margin = Math.Max(12.0, Math.Round(h * 0.11));
        var coverSide = Math.Max(40.0, h - 2 * margin);
        var pad = (h - coverSide) / 2.0;

        CoverBorder.Width = coverSide;
        CoverBorder.Height = coverSide;
        CoverBorder.Margin = new Thickness(pad, 0, 16, 0);

        RightPanel.Margin = new Thickness(0, 0, pad, 0);
    }

    private void OnProgressPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Bounds.Width > 0)
        {
            var pos = e.GetPosition(border);
            var percent = Math.Clamp(pos.X / border.Bounds.Width, 0.0, 1.0);
            viewModel.Seek(percent);
        }
    }
}
