namespace Batteries.Models;

public enum DeviceKind
{
    Computer,
    Mouse,
    Keyboard,
    Headphones,
    Phone
}

public record BatteriesModel(
    bool ShowPercentage = true,
    bool ShowPeripherals = true,
    bool ShowRemainingTime = true,
    bool LowPowerWarning = true,
    int MouseBattery = 85,
    int KeyboardBattery = 68,
    int HeadphonesBattery = 92
);
