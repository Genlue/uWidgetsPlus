using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Threading;
using Batteries.Locales;
using Batteries.Models;
using Batteries.Services;

namespace Batteries.ViewModels;

public class BatteryDeviceItem : INotifyPropertyChanged
{
    private string name;
    private int percentage;
    private bool isCharging;
    private DeviceKind kind;

    public BatteryDeviceItem(string name, DeviceKind kind, int percentage, bool isCharging)
    {
        this.name = name;
        this.kind = kind;
        this.percentage = Math.Clamp(percentage, 0, 100);
        this.isCharging = isCharging;
    }

    public string Name
    {
        get => name;
        set { if (name != value) { name = value; OnPropertyChanged(); } }
    }

    public DeviceKind Kind
    {
        get => kind;
        set { if (kind != value) { kind = value; OnPropertyChanged(); OnPropertyChanged(nameof(IconData)); } }
    }

    public int Percentage
    {
        get => percentage;
        set
        {
            var clamped = Math.Clamp(value, 0, 100);
            if (percentage != clamped)
            {
                percentage = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PercentText));
                OnPropertyChanged(nameof(ProgressFraction));
                OnPropertyChanged(nameof(StrokeDashOffset));
                OnPropertyChanged(nameof(StatusBrush));
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public bool IsCharging
    {
        get => isCharging;
        set
        {
            if (isCharging != value)
            {
                isCharging = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusBrush));
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public string PercentText => $"{Percentage}%";

    public double ProgressFraction => Percentage / 100.0;

    public double StrokeDashOffset => 30.16 * (1.0 - ProgressFraction);

    public IBrush StatusBrush
    {
        get
        {
            if (IsCharging) return new SolidColorBrush(Color.Parse("#34C759"));
            if (Percentage > 20) return new SolidColorBrush(Color.Parse("#34C759"));
            if (Percentage > 10) return new SolidColorBrush(Color.Parse("#FF9500"));
            return new SolidColorBrush(Color.Parse("#FF3B30"));
        }
    }

    public string StatusText => IsCharging ? "⚡ 充电中" : $"{Percentage}%";

    public string IconData => Kind switch
    {
        DeviceKind.Computer => "M20 18c1.1 0 1.99-.9 1.99-2L22 6c0-1.1-.9-2-2-2H4c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2H0v2h24v-2h-4zM4 6h16v10H4V6z",
        DeviceKind.Mouse => "M13 1.07V9h7c0-4.08-3.05-7.44-7-7.93zM4 15c0 4.42 3.58 8 8 8s8-3.58 8-8v-4H4v4zm7-13.93C7.05 1.56 4 4.92 4 9h7V1.07z",
        DeviceKind.Keyboard => "M20 5H4c-1.1 0-1.99.9-1.99 2L2 17c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2zm-9 3h2v2h-2V8zm0 3h2v2h-2v-2zM8 8h2v2H8V8zm0 3h2v2H8v-2zm-1 2H5v-2h2v2zm0-3H5V8h2v2zm9 7H8v-2h8v2zm0-4h-2v-2h2v2zm0-3h-2V8h2v2zm3 3h-2v-2h2v2zm0-3h-2V8h2v2z",
        DeviceKind.Headphones => "M12 3a9 9 0 0 0-9 9v7c0 1.66 1.34 3 3 3h1a2 2 0 0 0 2-2v-4a2 2 0 0 0-2-2H5v-2a7 7 0 0 1 14 0v2h-2a2 2 0 0 0-2 2v4a2 2 0 0 0 2 2h1c1.66 0 3-1.34 3-3v-7a9 9 0 0 0-9-9z",
        DeviceKind.Phone => "M17 1.01L7 1c-1.1 0-2 .9-2 2v18c0 1.1.9 2 2 2h10c1.1 0 2-.9 2-2V3c0-1.1-.9-1.99-2-1.99zM17 19H7V5h10v14z",
        _ => "M15.67 4H14V2h-4v2H8.33C7.6 4 7 4.6 7 5.33v15.33C7 21.4 7.6 22 8.33 22h7.33c.74 0 1.34-.6 1.34-1.33V5.33C17 4.6 16.4 4 15.67 4z"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
}

public class BatteriesViewModel : INotifyPropertyChanged
{
    private BatteriesModel model;
    private readonly DispatcherTimer timer;
    private BatteryDeviceItem mainDevice;
    private string summaryText = string.Empty;

    public ObservableCollection<BatteryDeviceItem> Devices { get; } = [];

    public BatteriesViewModel(BatteriesModel? initialModel = null)
    {
        model = initialModel ?? new BatteriesModel();

        mainDevice = new BatteryDeviceItem(Locale.Batteries_MainDevice, DeviceKind.Computer, 100, true);
        Devices.Add(mainDevice);

        if (model.ShowPeripherals)
        {
            Devices.Add(new BatteryDeviceItem(Locale.Batteries_Mouse, DeviceKind.Mouse, model.MouseBattery, false));
            Devices.Add(new BatteryDeviceItem(Locale.Batteries_Keyboard, DeviceKind.Keyboard, model.KeyboardBattery, false));
            Devices.Add(new BatteryDeviceItem(Locale.Batteries_Headphones, DeviceKind.Headphones, model.HeadphonesBattery, false));
        }

        timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        timer.Tick += (_, _) => PollPowerStatus();
        timer.Start();

        PollPowerStatus();
    }

    public BatteriesModel Model => model;

    public BatteryDeviceItem MainDevice
    {
        get => mainDevice;
        private set
        {
            if (mainDevice != value)
            {
                mainDevice = value;
                OnPropertyChanged();
            }
        }
    }

    public string SummaryText
    {
        get => summaryText;
        private set
        {
            if (summaryText != value)
            {
                summaryText = value;
                OnPropertyChanged();
            }
        }
    }

    public void UpdateModel(BatteriesModel newModel)
    {
        model = newModel;

        if (Devices.Count > 1 && !model.ShowPeripherals)
        {
            while (Devices.Count > 1) Devices.RemoveAt(1);
        }
        else if (Devices.Count == 1 && model.ShowPeripherals)
        {
            Devices.Add(new BatteryDeviceItem(Locale.Batteries_Mouse, DeviceKind.Mouse, model.MouseBattery, false));
            Devices.Add(new BatteryDeviceItem(Locale.Batteries_Keyboard, DeviceKind.Keyboard, model.KeyboardBattery, false));
            Devices.Add(new BatteryDeviceItem(Locale.Batteries_Headphones, DeviceKind.Headphones, model.HeadphonesBattery, false));
        }

        PollPowerStatus();
    }

    private void PollPowerStatus()
    {
        var info = PowerService.GetCurrentPowerInfo();

        mainDevice.Percentage = info.Percentage;
        mainDevice.IsCharging = info.IsCharging;

        if (!info.HasBattery)
        {
            mainDevice.Name = Locale.Batteries_AcPower;
            SummaryText = "已连接交流电源";
        }
        else if (info.IsCharging)
        {
            mainDevice.Name = Locale.Batteries_MainDevice;
            SummaryText = $"{Locale.Batteries_Charging} ({info.Percentage}%)";
        }
        else
        {
            mainDevice.Name = Locale.Batteries_MainDevice;
            SummaryText = info.RemainingMinutes > 0
                ? $"预计剩余 {(info.RemainingMinutes / 60)}小时{(info.RemainingMinutes % 60)}分"
                : $"剩余电量 {info.Percentage}%";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
}
