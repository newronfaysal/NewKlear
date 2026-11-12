using Microsoft.Win32;
using NewKlear.Core.Infrastructure;

namespace NewKlear.Core.Tweaks;

/// <summary>The value a registry setting had before NewKlear touched it.</summary>
public sealed class RegBackup
{
    public RegistryHive Hive { get; set; }
    public string Path { get; set; } = "";
    /// <summary>Null when this record tracks whether a whole key existed.</summary>
    public string? Name { get; set; }
    public bool Existed { get; set; }
    public RegistryValueKind Kind { get; set; }
    public string? Data { get; set; }

    public object? Restore() => Kind switch
    {
        _ when Data is null => null,
        RegistryValueKind.DWord => unchecked((int)uint.Parse(Data)),
        RegistryValueKind.QWord => long.Parse(Data),
        RegistryValueKind.Binary => Convert.FromBase64String(Data),
        RegistryValueKind.MultiString => Data.Split('\0'),
        _ => Data,
    };

    public static string? Serialize(object? value, RegistryValueKind kind) => value switch
    {
        null => null,
        int i when kind == RegistryValueKind.DWord => unchecked((uint)i).ToString(),
        long l => l.ToString(),
        byte[] b => Convert.ToBase64String(b),
        string[] arr => string.Join('\0', arr),
        _ => value.ToString(),
    };
}

/// <summary>
/// Persists the original registry state per tweak. The first backup is kept forever (until revert),
/// so applying a tweak twice never overwrites the true original.
/// </summary>
public static class TweakBackupStore
{
    private static readonly object Gate = new();

    private static Dictionary<string, List<RegBackup>> Load() =>
        JsonStore.Load<Dictionary<string, List<RegBackup>>>(AppPaths.TweakBackupFile);

    public static bool Has(string tweakId)
    {
        lock (Gate) return Load().ContainsKey(tweakId);
    }

    public static List<RegBackup>? Get(string tweakId)
    {
        lock (Gate) return Load().TryGetValue(tweakId, out var v) ? v : null;
    }

    public static void SaveIfMissing(string tweakId, List<RegBackup> backup)
    {
        lock (Gate)
        {
            var all = Load();
            if (all.ContainsKey(tweakId)) return;
            all[tweakId] = backup;
            JsonStore.Save(AppPaths.TweakBackupFile, all);
        }
    }

    public static void Remove(string tweakId)
    {
        lock (Gate)
        {
            var all = Load();
            if (all.Remove(tweakId)) JsonStore.Save(AppPaths.TweakBackupFile, all);
        }
    }
}
