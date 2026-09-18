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
    bool ShowRemainingTime = true
);
