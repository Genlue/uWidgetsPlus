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

    public static PowerInfo GetCurrentPowerInfo()
    {
        if (GetSystemPowerStatus(out var status))
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
}
