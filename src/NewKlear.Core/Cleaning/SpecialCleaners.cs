using Microsoft.Win32;
using NewKlear.Core.Infrastructure;

namespace NewKlear.Core.Cleaning;

public sealed class RecycleBinCleaner : CleanerBase
{
    public override Task<ScanResult> ScanAsync(CancellationToken ct)
    {
        var info = new NativeMethods.SHQUERYRBINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.SHQUERYRBINFO>() };
        var hr = NativeMethods.SHQueryRecycleBin(null, ref info);
        return Task.FromResult(hr == 0 ? new ScanResult(info.i64Size, (int)info.i64NumItems) : ScanResult.Empty);
    }

    public override async Task<CleanResult> CleanAsync(IProgress<string>? progress, CancellationToken ct)
    {
        var before = await ScanAsync(ct);
        if (before.Files == 0) return new CleanResult(0, 0, 0);
        progress?.Report("Recycle Bin");
        var hr = NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, null,
            NativeMethods.SHERB_NOCONFIRMATION | NativeMethods.SHERB_NOPROGRESSUI | NativeMethods.SHERB_NOSOUND);
        Log.Info($"Emptied Recycle Bin ({Format.Bytes(before.Bytes)}), hr=0x{hr:X}");
        return hr == 0 ? new CleanResult(before.Bytes, before.Files, 0) : new CleanResult(0, 0, before.Files, "Could not empty the Recycle Bin");
    }
}

/// <summary>
/// Cleaner backed by an external command (DISM, powercfg, wevtutil, Disk Cleanup).
/// Space freed is measured as the change in free space on the system drive.
/// </summary>
public sealed class CommandCleaner : CleanerBase
{
    public required Func<CancellationToken, Task<ScanResult>> Scan { get; init; }
    public required Func<IProgress<string>?, CancellationToken, Task<string?>> Run { get; init; }

    public override Task<ScanResult> ScanAsync(CancellationToken ct) => Scan(ct);

    public override async Task<CleanResult> CleanAsync(IProgress<string>? progress, CancellationToken ct)
    {
        Log.Info($"Running: {Name}");
        var before = ShellHelper.SystemDriveFreeBytes();
        var note = await Run(progress, ct);
        var after = ShellHelper.SystemDriveFreeBytes();
        var freed = Math.Max(0, after - before);
        Log.Info($"{Name} finished, freed {Format.Bytes(freed)}{(note is null ? "" : " — " + note)}");
        return new CleanResult(freed, 0, 0, note);
    }
}

/// <summary>Drives the built-in Disk Cleanup (cleanmgr) for handlers that need TrustedInstaller rights.</summary>
public static class DiskCleanupHelper
{
    private const string VolumeCaches = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VolumeCaches";
    private const int Slot = 4242;

    public static async Task<string?> RunAsync(string[] handlers, CancellationToken ct)
    {
        var flag = $"StateFlags{Slot:0000}";
        using var root = Registry.LocalMachine.OpenSubKey(VolumeCaches, writable: true);
        if (root is null) return "Disk Cleanup is not available on this PC";

        var present = new List<string>();
        foreach (var h in handlers)
        {
            using var key = root.OpenSubKey(h, writable: true);
            if (key is null) continue;
            key.SetValue(flag, 2, RegistryValueKind.DWord);
            present.Add(h);
        }
        if (present.Count == 0) return "Nothing to clean";

        try
        {
            await ProcessRunner.RunAsync(ProcessRunner.System32("cleanmgr.exe"), $"/sagerun:{Slot}", ct, TimeSpan.FromMinutes(90));
            return null;
        }
        finally
        {
            foreach (var h in present)
            {
                using var key = root.OpenSubKey(h, writable: true);
                key?.DeleteValue(flag, throwOnMissingValue: false);
            }
        }
    }
}
