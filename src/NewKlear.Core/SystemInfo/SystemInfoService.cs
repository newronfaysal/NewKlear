using Microsoft.Win32;
using NewKlear.Core.Infrastructure;

namespace NewKlear.Core.SystemInfo;

public sealed record DriveUsage(string Name, string Label, long Total, long Free)
{
    public long Used => Total - Free;
    public double UsedPercent => Total == 0 ? 0 : 100.0 * Used / Total;
}

public sealed record MemoryUsage(ulong Total, ulong Available)
{
    public ulong Used => Total - Available;
    public double UsedPercent => Total == 0 ? 0 : 100.0 * Used / Total;
}

public static class SystemInfoService
{
    public static IReadOnlyList<DriveUsage> GetDrives()
    {
        var list = new List<DriveUsage>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (d.DriveType != DriveType.Fixed || !d.IsReady) continue;
                list.Add(new DriveUsage(d.Name.TrimEnd('\\'), string.IsNullOrEmpty(d.VolumeLabel) ? "Local Disk" : d.VolumeLabel,
                    d.TotalSize, d.AvailableFreeSpace));
            }
            catch { /* not ready */ }
        }
        return list;
    }

    public static MemoryUsage GetMemory()
    {
        var m = new NativeMethods.MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>() };
        return NativeMethods.GlobalMemoryStatusEx(ref m) ? new MemoryUsage(m.ullTotalPhys, m.ullAvailPhys) : new MemoryUsage(0, 0);
    }

    public static TimeSpan Uptime => TimeSpan.FromMilliseconds(Environment.TickCount64);

    public static string OsName
    {
        get
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                var product = key?.GetValue("ProductName")?.ToString() ?? "Windows";
                var display = key?.GetValue("DisplayVersion")?.ToString() ?? "";
                var build = key?.GetValue("CurrentBuildNumber")?.ToString() ?? "";
                if (int.TryParse(build, out var b) && b >= 22000) product = product.Replace("Windows 10", "Windows 11");
                return $"{product} {display} (build {build})".Trim();
            }
            catch
            {
                return Environment.OSVersion.VersionString;
            }
        }
    }

    public static string CpuName
    {
        get
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                return key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? "Processor";
            }
            catch
            {
                return "Processor";
            }
        }
    }
}

/// <summary>Measures total CPU usage between calls using GetSystemTimes.</summary>
public sealed class CpuSampler
{
    private long _idle, _kernel, _user;

    public CpuSampler() => NativeMethods.GetSystemTimes(out _idle, out _kernel, out _user);

    public double Sample()
    {
        if (!NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user)) return 0;
        var idleD = idle - _idle;
        var total = (kernel - _kernel) + (user - _user); // kernel time includes idle
        _idle = idle; _kernel = kernel; _user = user;
        return total <= 0 ? 0 : Math.Clamp(100.0 * (total - idleD) / total, 0, 100);
    }
}
