using System;
using System.Runtime.InteropServices;

namespace Batteries.Services;

public static class PowerService
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;         // 0: Offline, 1: Online, 255: Unknown
        public byte BatteryFlag;          // 1: High, 2: Low, 4: Critical, 8: Charging, 128: No system battery, 255: Unknown
        public byte BatteryLifePercent;   // 0-100, 255: Unknown
        public byte SystemStatusFlag;
        public int BatteryLifeTime;       // Seconds remaining, -1 if unknown
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    public record PowerInfo(
        bool HasBattery,
        bool IsCharging,
        bool IsAcConnected,
        int Percentage,
        int RemainingMinutes
    );

    public record PeripheralInfo(
        string Name,
        Models.DeviceKind Kind,
        int Percentage
    );

    public static PowerInfo GetCurrentPowerInfo()
    {
        if (OperatingSystem.IsWindows() && GetSystemPowerStatus(out var status))
        {
            bool hasBattery = status.BatteryFlag != 128 && status.BatteryLifePercent != 255;
            bool isAc = status.ACLineStatus == 1;
            bool isCharging = (status.BatteryFlag & 8) != 0 || (isAc && hasBattery && status.BatteryLifePercent < 100);
            int percent = hasBattery ? Math.Clamp((int)status.BatteryLifePercent, 0, 100) : 100;
            int minutes = status.BatteryLifeTime > 0 ? status.BatteryLifeTime / 60 : -1;

            return new PowerInfo(hasBattery, isCharging, isAc, percent, minutes);
        }

        return new PowerInfo(false, true, true, 100, -1);
    }

    public static System.Collections.Generic.List<PeripheralInfo> GetConnectedPeripherals()
    {
        var list = new System.Collections.Generic.List<PeripheralInfo>();
        if (!OperatingSystem.IsWindows()) return list;

        try
        {
            string[] rootPaths = [
                @"SYSTEM\CurrentControlSet\Enum\BTHLEDevice",
                @"SYSTEM\CurrentControlSet\Enum\BTHENUM",
                @"SYSTEM\CurrentControlSet\Enum\HID"
            ];

            foreach (var rootPath in rootPaths)
            {
                using var root = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(rootPath);
                if (root == null) continue;

                foreach (var subKeyName in root.GetSubKeyNames())
                {
                    using var subKey = root.OpenSubKey(subKeyName);
                    if (subKey == null) continue;

                    foreach (var instanceName in subKey.GetSubKeyNames())
                    {
                        using var instanceKey = subKey.OpenSubKey(instanceName);
                        if (instanceKey == null) continue;

                        using var devParams = instanceKey.OpenSubKey("Device Parameters");
                        if (devParams == null) continue;

                        var batteryObj = devParams.GetValue("BatteryPercentage") ?? devParams.GetValue("BatteryLifePercent");
                        if (batteryObj is int or byte or short or long)
                        {
                            int percent = Convert.ToInt32(batteryObj);
                            if (percent >= 0 && percent <= 100)
                            {
                                var friendlyName = instanceKey.GetValue("FriendlyName") as string
                                    ?? instanceKey.GetValue("DeviceDesc") as string
                                    ?? subKeyName;

                                if (friendlyName.StartsWith("@"))
                                {
                                    var semi = friendlyName.IndexOf(';');
                                    if (semi >= 0 && semi < friendlyName.Length - 1)
                                        friendlyName = friendlyName[(semi + 1)..];
                                }

                                var kind = InferDeviceKind(friendlyName);
                                list.Add(new PeripheralInfo(friendlyName, kind, percent));
                            }
                        }
                    }
                }
            }
        }
        catch { }

        return list;
    }

    private static Models.DeviceKind InferDeviceKind(string name)
    {
        var lower = name.ToLowerInvariant();
        if (lower.Contains("mouse") || lower.Contains("鼠标")) return Models.DeviceKind.Mouse;
        if (lower.Contains("keyboard") || lower.Contains("键盘") || lower.Contains("kbd")) return Models.DeviceKind.Keyboard;
        if (lower.Contains("headset") || lower.Contains("headphone") || lower.Contains("earphone") ||
            lower.Contains("airpod") || lower.Contains("buds") || lower.Contains("audio") || lower.Contains("耳机"))
            return Models.DeviceKind.Headphones;
        if (lower.Contains("phone") || lower.Contains("手机")) return Models.DeviceKind.Phone;
        return Models.DeviceKind.Mouse;
    }
}
