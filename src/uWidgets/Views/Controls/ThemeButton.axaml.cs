using System;
using System.ComponentModel;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models.Settings;
using uWidgets.Locales;
using uWidgets.Services;

namespace uWidgets.Views.Controls;

public partial class ThemeButton : UserControl, INotifyPropertyChanged
{
    public new event PropertyChangedEventHandler? PropertyChanged;
    private static readonly Bitmap wallpaper = GetWallpaperPreview();
    public Theme AppTheme { get; }
    public Bitmap Wallpaper => wallpaper;
    public bool DimWallpaper => AppTheme.DarkMode == true;
    public bool IsLiquidGlass => AppTheme.IsLiquidGlass;
    public bool IsFrosted => AppTheme.UsesNativeBlur;
    public bool IsSelected => appSettingsProvider.Get().Theme.EffectiveSurface == AppTheme.EffectiveSurface
        || (IsFrosted && appSettingsProvider.Get().Theme.EffectiveSurface == SurfaceStyle.OutlinedAcrylic);
    public IBrush SelectionBrush => IsSelected ? Brushes.DodgerBlue : Brushes.Transparent;
    public Theme GlassMaterial => appSettingsProvider.Get().Theme with
    {
        Surface = SurfaceStyle.LiquidGlass,
        OpacityLevel = IsSelected ? appSettingsProvider.Get().Theme.OpacityLevel : AppTheme.OpacityLevel
    };
    public Brush WidgetBackground
    {
        get
        {
            var theme = appSettingsProvider.Get().Theme;
            var dark = IsDark();
            // Both surfaces use the user's custom dark/light background colors
            // (defaults: dark #2E2E2E, light #FFFFFF).
            var color = dark
                ? ParseColor(theme.EffectiveSolidBackgroundDark, "#2E2E2E")
                : ParseColor(theme.EffectiveSolidBackgroundLight, "#FFFFFF");
            return new SolidColorBrush(color, AppTheme.OpacityLevel);
        }
    }

    /// <summary>
    /// Whether the preset preview represents the dark mode: the template's own
    /// choice, or — when it follows the system — the currently resolved variant.
    /// </summary>
    private bool IsDark() =>
        AppTheme.DarkMode ?? Application.Current!.ActualThemeVariant == ThemeVariant.Dark;
    public CornerRadius WidgetCornerRadius => new(WidgetRadius);
    public double WidgetRadius => AppTheme.UseNativeFrame ? 2 : 10;
    public BoxShadows WidgetShadow => AppTheme.UseNativeFrame 
        ? new(BoxShadow.Parse("0 0 10 0 #40000000"))
        : new();
    public FontFamily WidgetFontFamily => AppTheme.FontFamily == "Inter"
        ? new FontFamily("avares://Avalonia.Fonts.Inter#Inter")
        : new FontFamily(AppTheme.FontFamily);

    /// <summary>The preset name shown below the preview (毛玻璃 / 纯色).</summary>
    public string ThemeName => IsLiquidGlass ? Locale.Settings_Appearance_Surface_LiquidGlass : AppTheme.IsGlass
        ? Locale.Settings_Appearance_Surface_Frosted
        : Locale.Settings_Appearance_Surface_Solid;

    private static Color ParseColor(string hex, string fallbackHex) =>
        Color.TryParse(hex, out var color) ? color : Color.Parse(fallbackHex);

    /// <summary>
    /// Highlight ring of the preset preview: the outline is an option of the glass
    /// theme (drawn when <see cref="Theme.OutlineWidth"/> &gt; 0), with the live
    /// color/width so the preview reflects what the user configured. Strongest at
    /// the top-left and bottom-right corners, fading along every edge to nothing at
    /// the top-right and bottom-left (conic gradient; square card → 315° start).
    /// </summary>
    public IBrush? PreviewBorderBrush =>
        AppTheme.IsGlass && appSettingsProvider.Get().Theme.OutlineWidth > 0
            ? BuildPreviewOutline()
            : AppTheme.UseNativeFrame ? new SolidColorBrush(Color.Parse("#60808080")) : null;

    private ConicGradientBrush BuildPreviewOutline()
    {
        var theme = appSettingsProvider.Get().Theme;
        var color = Color.TryParse(theme.EffectiveOutlineColor, out var parsed)
            ? parsed
            : Color.Parse(uWidgets.Core.Models.Settings.Theme.DefaultOutlineColor);

        return new ConicGradientBrush
        {
            Angle = 315,
            Center = RelativePoint.Center,
            GradientStops =
            {
                new GradientStop(color, 0),
                new GradientStop(Colors.Transparent, 0.25),
                new GradientStop(color, 0.5),
                new GradientStop(Colors.Transparent, 0.75),
                new GradientStop(color, 1.0)
            }
        };
    }

    public Thickness PreviewBorderThickness => AppTheme.UseNativeFrame
        ? new Thickness(1)
        : AppTheme.IsGlass && appSettingsProvider.Get().Theme.OutlineWidth > 0
            ? new Thickness(Math.Clamp(appSettingsProvider.Get().Theme.OutlineWidth, 0, 6))
            : new Thickness(0);

    public SolidColorBrush? WidgetForeground
    {
        get
        {
            var theme = appSettingsProvider.Get().Theme;
            var dark = IsDark();
            // 黑白 monochrome: white in dark mode, black in light mode.
            if (theme.Monochrome && theme.EffectiveMonochromeVariant == MonochromeStyle.BlackWhite)
                return new SolidColorBrush(dark ? Colors.White : Colors.Black);
            return new SolidColorBrush((Color)Application.Current!.FindResource(dark ? "SystemAccentColorLight2" : "SystemAccentColorDark1")!);
        }
    }
    
    private readonly IAppSettingsProvider appSettingsProvider;

    public ThemeButton(IAppSettingsProvider appSettingsProvider, Theme appTheme)
    {
        this.appSettingsProvider = appSettingsProvider;
        AppTheme = appTheme;
        DataContext = this;
        InitializeComponent();
        AttachedToVisualTree += (_, _) =>
        {
            appSettingsProvider.DataChanged += OnSettingsChanged;
            RefreshPreview();
        };
        DetachedFromVisualTree += (_, _) => appSettingsProvider.DataChanged -= OnSettingsChanged;
    }

    private void OnSettingsChanged(object sender, AppSettings? oldData, AppSettings newData) => RefreshPreview();

    private void RefreshPreview()
    {
        foreach (var property in new[] { nameof(IsSelected), nameof(SelectionBrush), nameof(GlassMaterial),
            nameof(WidgetBackground), nameof(WidgetForeground), nameof(PreviewBorderBrush), nameof(PreviewBorderThickness) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }
    
    public static Bitmap GetWallpaperPreview(int targetHeight = 150)
    {
        // 1. Fast path: Decode directly from the Windows wallpaper file in milliseconds
        try
        {
            var wallpaperPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Themes\TranscodedWallpaper");
            if (!File.Exists(wallpaperPath))
                wallpaperPath = InteropService.GetWallpaperPath();

            if (File.Exists(wallpaperPath))
            {
                using var fileStream = File.OpenRead(wallpaperPath);
                return Bitmap.DecodeToHeight(fileStream, targetHeight);
            }
        }
        catch { }

        // 2. Fallback: query wallpaper snapshot
        var snapshot = LiquidGlassWallpaper.Get();
        try
        {
            if (snapshot.ImageBytes != null)
            {
                using var stream = new MemoryStream(snapshot.ImageBytes);
                return Bitmap.DecodeToHeight(stream, targetHeight);
            }
        }
        catch (Exception) { /* No readable wallpaper: preview the desktop solid color. */ }
        var renderTarget = new RenderTargetBitmap(new PixelSize(targetHeight * 4 / 3, targetHeight), new Vector(96, 96));
        using (var context = renderTarget.CreateDrawingContext(false))
            context.FillRectangle(new SolidColorBrush(Color.FromRgb(snapshot.Background.Red, snapshot.Background.Green, snapshot.Background.Blue)),
                new Rect(0, 0, targetHeight * 4 / 3, targetHeight));
        return renderTarget;
    }

    /// <summary>
    /// Applies only the surface material (glass vs solid). Dark mode, accent color,
    /// monochrome, font and native frame are set by their own controls and preserved,
    /// so choosing a material no longer resets the user's other appearance choices.
    /// </summary>
    private void Apply(object? sender, RoutedEventArgs e)
    {
        var settings = appSettingsProvider.Get();
        if (IsSelected) return;
        var newTheme = settings.Theme with
        {
            Surface = AppTheme.EffectiveSurface,
            OpacityLevel = AppTheme.OpacityLevel
        };
        appSettingsProvider.Save(settings with { Theme = newTheme });
    }
}
