using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using uWidgets.Core.Interfaces;
using uWidgets.Services;
using uWidgets.ViewModels;

namespace uWidgets.Views.Pages;

public partial class General : UserControl
{
    public General(IAppSettingsProvider appSettingsProvider, UpdateService? updateService = null)
    {
        DataContext = new GeneralViewModel(appSettingsProvider, updateService);
        InitializeComponent();
    }
}