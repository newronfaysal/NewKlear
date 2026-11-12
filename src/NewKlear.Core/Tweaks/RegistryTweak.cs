using Microsoft.Win32;
using NewKlear.Core.Cleaning;
using NewKlear.Core.Infrastructure;

namespace NewKlear.Core.Tweaks;

/// <summary>
/// A tweak made of registry values. Before applying, the original state is saved (JSON for exact revert
/// plus a .reg export of each key for manual recovery). Revert restores the original, or Windows defaults
/// if no backup exists.
/// </summary>
public class RegistryTweak : ITweak
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required TweakCategory Category { get; init; }
    public Risk Risk { get; init; } = Risk.Safe;
    public ApplyEffect Effect { get; init; } = ApplyEffect.Immediate;
    public required IReadOnlyList<RegValue> Values { get; init; }

    /// <summary>Keys created by the tweak that should be removed entirely on revert if they didn't exist before.</summary>
    public IReadOnlyList<(RegistryHive Hive, string Path)> OwnedKeys { get; init; } = [];

    public Func<Task>? AfterChange { get; init; }

    public virtual bool IsAvailable => true;
    public bool RequiresAdmin => Values.Any(v => v.Hive == RegistryHive.LocalMachine);

    public virtual bool IsApplied()
    {
        try
        {
            return Values.All(v => RegistryHelper.ValuesEqual(RegistryHelper.Read(v.Hive, v.Path, v.Name, out _), v.Applied));
        }
        catch
        {
            return false;
        }
    }

    public virtual async Task ApplyAsync()
    {
        if (!TweakBackupStore.Has(Id))
        {
            var backup = new List<RegBackup>();
            foreach (var v in Values)
            {
                var current = RegistryHelper.Read(v.Hive, v.Path, v.Name, out var kind);
                backup.Add(new RegBackup
                {
                    Hive = v.Hive, Path = v.Path, Name = v.Name, Existed = current is not null,
                    Kind = current is null ? v.Kind : kind, Data = RegBackup.Serialize(current, kind),
                });
            }
            foreach (var (hive, path) in OwnedKeys)
                backup.Add(new RegBackup { Hive = hive, Path = path, Name = null, Existed = RegistryHelper.KeyExists(hive, path) });

            await ExportRegFilesAsync();
            TweakBackupStore.SaveIfMissing(Id, backup);
        }

        foreach (var v in Values)
            RegistryHelper.Write(v.Hive, v.Path, v.Name, v.Kind, v.Applied);

        Log.Info($"Applied tweak '{Name}'");
        if (AfterChange != null) await AfterChange();
        NativeMethods.BroadcastSettingChange();
    }

    public virtual async Task RevertAsync()
    {
        var backup = TweakBackupStore.Get(Id);
        if (backup != null)
        {
            foreach (var b in backup.Where(b => b.Name != null))
            {
                if (b.Existed) RegistryHelper.Write(b.Hive, b.Path, b.Name!, b.Kind, b.Restore()!);
                else RegistryHelper.Delete(b.Hive, b.Path, b.Name!);
            }
            foreach (var b in backup.Where(b => b.Name == null && !b.Existed))
                RegistryHelper.DeleteKeyTree(b.Hive, b.Path);
        }
        else
        {
            foreach (var v in Values)
            {
                if (v.DefaultValue is null) RegistryHelper.Delete(v.Hive, v.Path, v.Name);
                else RegistryHelper.Write(v.Hive, v.Path, v.Name, v.Kind, v.DefaultValue);
            }
            foreach (var (hive, path) in OwnedKeys) RegistryHelper.DeleteKeyTree(hive, path);
        }

        TweakBackupStore.Remove(Id);
        Log.Info($"Reverted tweak '{Name}'{(backup is null ? " to Windows defaults" : " to original values")}");
        if (AfterChange != null) await AfterChange();
        NativeMethods.BroadcastSettingChange();
    }

    /// <summary>Human-readable .reg backups (one per key) for manual recovery.</summary>
    private async Task ExportRegFilesAsync()
    {
        var keys = Values.Select(v => (v.Hive, v.Path)).Distinct();
        var reg = ProcessRunner.System32("reg.exe");
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var i = 0;
        foreach (var (hive, path) in keys)
        {
            if (!RegistryHelper.KeyExists(hive, path)) continue;
            var root = hive == RegistryHive.CurrentUser ? "HKCU" : "HKLM";
            var file = Path.Combine(AppPaths.Backups, $"{stamp}_{Id}_{i++}.reg");
            try
            {
                await ProcessRunner.RunAsync(reg, $"export \"{root}\\{path}\" \"{file}\" /y", timeout: TimeSpan.FromSeconds(30));
            }
            catch (Exception ex)
            {
                Log.Warn($"reg export failed for {root}\\{path}: {ex.Message}");
            }
        }
    }
}

/// <summary>Changes the start type of one or more Windows services (Start: 2 auto, 3 manual, 4 disabled).</summary>
public sealed class ServiceTweak : RegistryTweak
{
    private const string ServicesPath = @"SYSTEM\CurrentControlSet\Services\";

    public required IReadOnlyList<string> Services { get; init; }

    public override bool IsAvailable => Values.Count > 0;

    /// <param name="services">Service name and its Windows default start type.</param>
    public static ServiceTweak Create(string id, string name, string description, TweakCategory category, Risk risk,
        params (string Service, int DefaultStart)[] services)
    {
        var present = services.Where(s => RegistryHelper.KeyExists(RegistryHive.LocalMachine, ServicesPath + s.Service)).ToList();
        return new ServiceTweak
        {
            Id = id, Name = name, Description = description, Category = category, Risk = risk,
            Effect = ApplyEffect.Immediate,
            Services = present.Select(s => s.Service).ToList(),
            Values = present.Select(s => new RegValue(RegistryHive.LocalMachine, ServicesPath + s.Service, "Start",
                RegistryValueKind.DWord, 4, s.DefaultStart)).ToList(),
        };
    }

    public override async Task ApplyAsync()
    {
        await base.ApplyAsync();
        foreach (var s in Services) await ServiceHelper.StopAsync(s);
    }

    public override async Task RevertAsync()
    {
        await base.RevertAsync();
        foreach (var s in Services)
        {
            if (RegistryHelper.Read(RegistryHive.LocalMachine, ServicesPath + s, "Start", out _) is int start && start == 2)
                await ServiceHelper.StartAsync(s);
        }
    }
}
