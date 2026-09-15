using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Media;
using FixedWidgets.Models;
using FixedWidgets.Services;
using ReactiveUI;
using uWidgets.Services;

namespace FixedWidgets.ViewModels;

public class AggregateViewModel : ReactiveObject, IDisposable
{
    /// <summary>
    /// The weekday name is formatted on every one-second tick, so the culture is resolved
    /// once here: <see cref="CultureInfo.GetCultureInfo(string)"/> hands back a cached,
    /// read-only instance instead of building (and unifying) a new one per tick.
    /// </summary>
    private static readonly CultureInfo ChineseCulture = CultureInfo.GetCultureInfo("zh-CN");

    private readonly OpenMeteoWeatherService weatherService = new();
    private AggregateModel model;

    /// <summary>Set once <see cref="Dispose"/> ran; keeps disposal (and late replies) harmless.</summary>
    private bool disposed;

    private string cityName = "北京";
    private string temperatureText = "24°";
    private string conditionText = "晴朗";
    private string highLowText = "↑26°  ↓18°";
    private StreamGeometry? weatherIcon = OpenMeteoWeatherService.GetWeatherIcon(0);
    private string timeText = "00:00";
    private string dateText = "";
    private string monthYearText = "";
    private IReadOnlyList<string> weekHeaders = ["一", "二", "三", "四", "五", "六", "日"];
    private IReadOnlyList<DayCellViewModel> dayCells = [];
    private IBrush? customAccentBrush;

    public string CityName
    {
        get => cityName;
        set => this.RaiseAndSetIfChanged(ref cityName, value);
    }

    public string TemperatureText
    {
        get => temperatureText;
        set => this.RaiseAndSetIfChanged(ref temperatureText, value);
    }

    public string ConditionText
    {
        get => conditionText;
        set => this.RaiseAndSetIfChanged(ref conditionText, value);
    }

    public string HighLowText
    {
        get => highLowText;
        set => this.RaiseAndSetIfChanged(ref highLowText, value);
    }

    public StreamGeometry? WeatherIcon
    {
        get => weatherIcon;
        set => this.RaiseAndSetIfChanged(ref weatherIcon, value);
    }

    public string TimeText
    {
        get => timeText;
        set => this.RaiseAndSetIfChanged(ref timeText, value);
    }

    public string DateText
    {
        get => dateText;
        set => this.RaiseAndSetIfChanged(ref dateText, value);
    }

    public string MonthYearText
    {
        get => monthYearText;
        set => this.RaiseAndSetIfChanged(ref monthYearText, value);
    }

    public IReadOnlyList<string> WeekHeaders
    {
        get => weekHeaders;
        set => this.RaiseAndSetIfChanged(ref weekHeaders, value);
    }

    public IReadOnlyList<DayCellViewModel> DayCells
    {
        get => dayCells;
        set => this.RaiseAndSetIfChanged(ref dayCells, value);
    }

    public IBrush? CustomAccentBrush
    {
        get => customAccentBrush;
        set => this.RaiseAndSetIfChanged(ref customAccentBrush, value);
    }

    public AggregateViewModel(AggregateModel model)
    {
        this.model = model;
        ApplyModel(model);

        TimerService.Timer1Second.Subscribe(OnSecondTick);
        TimerService.Timer1Hour.Subscribe(OnHourTick);

        UpdateClock();
        UpdateCalendar();
        _ = UpdateWeather();
    }

    public void UpdateModel(AggregateModel newModel)
    {
        model = newModel;
        ApplyModel(newModel);
        UpdateClock();
        UpdateCalendar();
        _ = UpdateWeather();
    }

    private void ApplyModel(AggregateModel m)
    {
        CityName = m.City;
        if (!string.IsNullOrWhiteSpace(m.CustomAccentColor) && Color.TryParse(m.CustomAccentColor, out var col))
        {
            CustomAccentBrush = new SolidColorBrush(col);
        }
        else
        {
            CustomAccentBrush = null;
        }

        // Set week headers according to FirstDayOfWeek
        if (m.FirstDayOfWeek == DayOfWeek.Sunday)
        {
            WeekHeaders = ["日", "一", "二", "三", "四", "五", "六"];
        }
        else
        {
            WeekHeaders = ["一", "二", "三", "四", "五", "六", "日"];
        }
    }

    private void OnSecondTick()
    {
        UpdateClock();
    }

    private void OnHourTick()
    {
        UpdateCalendar();
        _ = UpdateWeather();
    }

    public void UpdateClock()
    {
        var now = DateTime.Now;

        // Time format
        if (model.Is24Hour)
        {
            TimeText = model.ShowSeconds ? now.ToString("HH:mm:ss") : now.ToString("HH:mm");
        }
        else
        {
            string tt = now.ToString("hh:mm", CultureInfo.InvariantCulture);
            string ampm = now.Hour >= 12 ? "PM" : "AM";
            TimeText = model.ShowSeconds ? $"{tt}:{now:ss} {ampm}" : $"{tt} {ampm}";
        }

        // Chinese Date & Weekday: e.g. "9月12日 星期六"
        string weekday = now.ToString("dddd", ChineseCulture);
        DateText = $"{now.Month}月{now.Day}日 {weekday}";
    }

    public void UpdateCalendar()
    {
        var now = DateTime.Now;
        MonthYearText = $"{now.Year}年 {now.Month}月";

        var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);
        int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);

        // Determine offset based on FirstDayOfWeek
        int startDayOffset = ((int)firstDayOfMonth.DayOfWeek - (int)model.FirstDayOfWeek + 7) % 7;

        var cells = new List<DayCellViewModel>(42);

        // Previous month padding days
        var prevMonthDate = firstDayOfMonth.AddMonths(-1);
        int daysInPrevMonth = DateTime.DaysInMonth(prevMonthDate.Year, prevMonthDate.Month);

        for (int i = startDayOffset - 1; i >= 0; i--)
        {
            int dayNum = daysInPrevMonth - i;
            var dt = new DateTime(prevMonthDate.Year, prevMonthDate.Month, dayNum);
            bool isWeekend = dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday;
            cells.Add(new DayCellViewModel(dayNum, dayNum.ToString(), false, false, isWeekend, dt));
        }

        // Current month days
        for (int day = 1; day <= daysInMonth; day++)
        {
            var dt = new DateTime(now.Year, now.Month, day);
            bool isToday = day == now.Day && now.Month == DateTime.Today.Month && now.Year == DateTime.Today.Year;
            bool isWeekend = dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday;
            cells.Add(new DayCellViewModel(day, day.ToString(), true, isToday, isWeekend, dt));
        }

        // Next month padding days to fill 35 or 42 grid cells (5 or 6 rows)
        int targetTotalCells = cells.Count > 35 ? 42 : 35;
        var nextMonthDate = firstDayOfMonth.AddMonths(1);
        int nextDay = 1;
        while (cells.Count < targetTotalCells)
        {
            var dt = new DateTime(nextMonthDate.Year, nextMonthDate.Month, nextDay);
            bool isWeekend = dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday;
            cells.Add(new DayCellViewModel(nextDay, nextDay.ToString(), false, false, isWeekend, dt));
            nextDay++;
        }

        DayCells = cells;
    }

    /// <summary>
    /// Refresh the weather block. Called from the constructor, from model updates and from the
    /// hourly tick (never as an event handler), so it returns a task the callers may discard —
    /// and it catches everything itself: an unhandled exception from an <c>async void</c>
    /// method would otherwise reach the dispatcher and take the whole process down.
    /// </summary>
    public async Task UpdateWeather()
    {
        try
        {
            var info = await weatherService.GetWeatherAsync(model.Latitude, model.Longitude, model.TemperatureUnit);

            // A reply that arrives after disposal (the view was unloaded mid-request) must not
            // touch this view model any more, and must not revive it through its bindings.
            if (disposed) return;

            if (info == null)
            {
                if (TemperatureText == "--°")
                {
                    TemperatureText = "24°";
                    ConditionText = "晴朗";
                    HighLowText = "↑26°  ↓18°";
                    WeatherIcon = OpenMeteoWeatherService.GetWeatherIcon(0);
                }
                return;
            }

            TemperatureText = $"{Math.Round(info.Temperature):0}°";
            ConditionText = info.ConditionText;
            HighLowText = $"↑{Math.Round(info.MaxTemp):0}°  ↓{Math.Round(info.MinTemp):0}°";
            WeatherIcon = info.Icon;
        }
        catch
        {
            // Offline or a service error: keep the values that are already on screen.
        }
    }

    /// <summary>
    /// Release the process-lifetime timer subscriptions and the weather service's
    /// <see cref="HttpClient"/>. Without this every abandoned view (each settings save
    /// re-creates the widget, each Gallery visit creates a preview) would keep ticking and
    /// holding a client alive forever. The view must build a fresh view model before using
    /// this one again. Idempotent.
    /// </summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        TimerService.Timer1Second.Unsubscribe(OnSecondTick);
        TimerService.Timer1Hour.Unsubscribe(OnHourTick);
        weatherService.Dispose();
        GC.SuppressFinalize(this);
    }
}
