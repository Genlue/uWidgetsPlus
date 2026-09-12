using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Media;
using FixedWidgets.Models;
using FixedWidgets.ViewModels;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;

namespace FixedWidgets.Views;

public partial class AggregateView : UserControl, IFixedSizeWidget, IWidgetSelfRefreshing
{
    private readonly AggregateViewModel viewModel;

    // IFixedSizeWidget implementation: strictly restrict to 4×2 and its proportional integer scalings
    public IReadOnlyList<(int Columns, int Rows)> AllowedBaseSpans { get; } = [(4, 2)];

    public IReadOnlyList<(int Columns, int Rows)> PresetSpans { get; } = [(4, 2), (8, 4)];

    public bool IsAllowedSpan(int columns, int rows) =>
        columns >= 4 && rows >= 2 && columns % 4 == 0 && rows % 2 == 0 && (columns / 4) == (rows / 2);

    public (int Columns, int Rows) SnapSpan(int columns, int rows)
    {
        int k = Math.Max(1, (int)Math.Round((columns / 4.0 + rows / 2.0) / 2.0));
        return (k * 4, k * 2);
    }

    public AggregateView() : this(new AggregateModel())
    {
    }

    public AggregateView(AggregateModel model)
    {
        viewModel = new AggregateViewModel(model);
        DataContext = viewModel;
        InitializeComponent();

        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        Unloaded -= OnUnloaded;
        viewModel.Dispose();
    }

    public void Refresh(WidgetLayout layout)
    {
        var model = layout.GetModel<AggregateModel>() ?? new AggregateModel();
        viewModel.UpdateModel(model);
    }
}
