using Microsoft.Win32;

namespace NewKlear.Core.Tweaks;

/// <summary>One registry value a tweak sets. <see cref="DefaultValue"/> null means "value absent" (Windows default).</summary>
public sealed record RegValue(RegistryHive Hive, string Path, string Name, RegistryValueKind Kind, object Applied, object? DefaultValue)
{
    public string Display => $"{(Hive == RegistryHive.CurrentUser ? "HKCU" : "HKLM")}\\{Path}\\{(Name.Length == 0 ? "(Default)" : Name)}";
}

public static class RegistryHelper
{
    public static RegistryKey Base(RegistryHive hive) => RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);

    public static object? Read(RegistryHive hive, string path, string name, out RegistryValueKind kind)
    {
        kind = RegistryValueKind.Unknown;
        using var key = Base(hive).OpenSubKey(path);
        if (key is null) return null;
        var value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value is not null) kind = key.GetValueKind(name);
        return value;
    }

    public static bool KeyExists(RegistryHive hive, string path)
    {
        using var key = Base(hive).OpenSubKey(path);
        return key is not null;
    }

    public static void Write(RegistryHive hive, string path, string name, RegistryValueKind kind, object value)
    {
        using var key = Base(hive).CreateSubKey(path, writable: true);
        key.SetValue(name, ToRegistry(value, kind), kind);
    }

    public static void Delete(RegistryHive hive, string path, string name)
    {
        using var key = Base(hive).OpenSubKey(path, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }

    public static void DeleteKeyTree(RegistryHive hive, string path)
    {
        Base(hive).DeleteSubKeyTree(path, throwOnMissingSubKey: false);
    }

    /// <summary>Normalises numeric types: DWORDs must be stored as int (with wrap-around for values over int.MaxValue).</summary>
    private static object ToRegistry(object value, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.DWord => value is int i ? i : unchecked((int)Convert.ToUInt32(value)),
        RegistryValueKind.QWord => unchecked((long)Convert.ToUInt64(value)),
        _ => value,
    };

    public static bool ValuesEqual(object? current, object expected)
    {
        if (current is null) return false;
        if (expected is byte[] eb) return current is byte[] cb && cb.AsSpan().SequenceEqual(eb);
        if (expected is string es) return string.Equals(current.ToString(), es, StringComparison.OrdinalIgnoreCase);
        try
        {
            var c = current is int ci ? unchecked((uint)ci) : Convert.ToUInt64(current);
            var e = expected is int ei ? unchecked((uint)ei) : Convert.ToUInt64(expected);
            return c == e;
        }
        catch
        {
            return Equals(current, expected);
        }
    }
}
