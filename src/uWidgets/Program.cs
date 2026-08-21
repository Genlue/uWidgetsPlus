using Avalonia;
using System;
using System.IO;
using uWidgets.Core;
using uWidgets.Core.Services;
using uWidgets.Services;

namespace uWidgets;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // Single-file mode: extract the embedded widget bundle & default settings first,
            // so the rest of the app sees a regular data folder. No-op in portable mode.
            WidgetBundle.ExtractIfNeeded();

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception e)
        {
            var fileName = Path.Combine(Const.DataFolder, "crash_log.txt");
            File.WriteAllText(fileName, $"{e.Message}{Environment.NewLine}{e.StackTrace}");
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var settings = new AppSettingsProvider().Get();
        
        return AppBuilder.Configure<App>()
            .UseWin32()
            .UsePlatformDetect()
            .WithInterFont()
            .With(new Win32PlatformOptions
            {
                CompositionMode = new[] { Win32CompositionMode.WinUIComposition },
                WinUICompositionBackdropCornerRadius = settings.Theme.UseNativeFrame ? 0 : settings.Dimensions.Radius
            })
            .LogToTrace();
    }
}