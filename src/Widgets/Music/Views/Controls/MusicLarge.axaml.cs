using Avalonia.Controls;
using Avalonia.Input;
using Music.Models;
using Music.ViewModels;

namespace Music.Views.Controls;

public partial class MusicLarge : UserControl
{
    private readonly MusicViewModel viewModel;
    public MusicLarge() : this(new MusicViewModel(new MusicModel())) { }

    public MusicLarge(MusicViewModel viewModel)
    {
        this.viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
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
