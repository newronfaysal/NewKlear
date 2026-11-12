using System.Diagnostics;
using Microsoft.Win32;
using NewKlear.Core.Infrastructure;

namespace NewKlear.Core.Startup;

public enum StartupSource
{
    UserRegistry,
    MachineRegistry,
    MachineRegistry32,
    UserStartupFolder,
    CommonStartupFolder,
}

public sealed class StartupEntry
{
    public required string Name { get; init; }
    public required string Command { get; init; }
    public required StartupSource Source { get; init; }
    public bool Enabled { get; set; }
    public string Publisher { get; init; } = "";
    public string? FilePath { get; init; }

    public bool RequiresAdmin => Source is StartupSource.MachineRegistry or StartupSource.MachineRegistry32 or StartupSource.CommonStartupFolder;

    public string SourceLabel => Source switch
    {
        StartupSource.UserRegistry => "Registry (current user)",
        StartupSource.MachineRegistry => "Registry (all users)",
        StartupSource.MachineRegistry32 => "Registry (all users, 32-bit)",
        StartupSource.UserStartupFolder => "Startup folder (current user)",
        StartupSource.CommonStartupFolder => "Startup folder (all users)",
        _ => "",
    };
}

/// <summary>
/// Reads and toggles startup programs using the same StartupApproved flags as Task Manager,
/// so nothing is deleted and Task Manager always shows the same state.
/// </summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Run32Key = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedRoot = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

    public static IReadOnlyList<StartupEntry> GetEntries()
    {
        var list = new List<StartupEntry>();
        ReadRegistry(list, RegistryHive.CurrentUser, RunKey, StartupSource.UserRegistry);
        ReadRegistry(list, RegistryHive.LocalMachine, RunKey, StartupSource.MachineRegistry);
        ReadRegistry(list, RegistryHive.LocalMachine, Run32Key, StartupSource.MachineRegistry32);
        ReadFolder(list, Environment.GetFolderPath(Environment.SpecialFolder.Startup), StartupSource.UserStartupFolder);
        ReadFolder(list, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), StartupSource.CommonStartupFolder);
        return list.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static RegistryKey Base(RegistryHive hive) => RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);

    private static (RegistryHive Hive, string SubKey) ApprovedLocation(StartupSource source) => source switch
    {
        StartupSource.UserRegistry => (RegistryHive.CurrentUser, ApprovedRoot + @"\Run"),
        StartupSource.MachineRegistry => (RegistryHive.LocalMachine, ApprovedRoot + @"\Run"),
        StartupSource.MachineRegistry32 => (RegistryHive.LocalMachine, ApprovedRoot + @"\Run32"),
        StartupSource.UserStartupFolder => (RegistryHive.CurrentUser, ApprovedRoot + @"\StartupFolder"),
        StartupSource.CommonStartupFolder => (RegistryHive.LocalMachine, ApprovedRoot + @"\StartupFolder"),
        _ => throw new ArgumentOutOfRangeException(nameof(source)),
    };

    private static bool IsApproved(StartupSource source, string valueName)
    {
        var (hive, sub) = ApprovedLocation(source);
        using var key = Base(hive).OpenSubKey(sub);
        if (key?.GetValue(valueName) is byte[] { Length: > 0 } data)
            return (data[0] & 1) == 0; // even = enabled (02/06), odd = disabled (03/07)
        return true;
    }

    private static void ReadRegistry(List<StartupEntry> list, RegistryHive hive, string path, StartupSource source)
    {
        try
        {
            using var key = Base(hive).OpenSubKey(path);
            if (key is null) return;
            foreach (var name in key.GetValueNames())
            {
                if (string.IsNullOrEmpty(name)) continue;
                var cmd = key.GetValue(name)?.ToString() ?? "";
                var exe = ExtractExecutable(cmd);
                list.Add(new StartupEntry
                {
                    Name = name, Command = cmd, Source = source, FilePath = exe,
                    Enabled = IsApproved(source, name), Publisher = GetPublisher(exe),
                });
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not read startup key {hive}\\{path}: {ex.Message}");
        }
    }

    private static void ReadFolder(List<StartupEntry> list, string folder, StartupSource source)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
        foreach (var file in Directory.EnumerateFiles(folder))
        {
            var fileName = Path.GetFileName(file);
            if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
            list.Add(new StartupEntry
            {
                Name = Path.GetFileNameWithoutExtension(file), Command = file, Source = source, FilePath = file,
                Enabled = IsApproved(source, fileName), Publisher = "",
            });
        }
    }

    public static void SetEnabled(StartupEntry entry, bool enabled)
    {
        var (hive, sub) = ApprovedLocation(entry.Source);
        var valueName = entry.Source is StartupSource.UserStartupFolder or StartupSource.CommonStartupFolder
            ? Path.GetFileName(entry.Command)
            : entry.Name;

        var data = new byte[12];
        data[0] = enabled ? (byte)0x02 : (byte)0x03;
        if (!enabled) BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(data, 4);

        using var key = Base(hive).CreateSubKey(sub, writable: true);
        key.SetValue(valueName, data, RegistryValueKind.Binary);
        entry.Enabled = enabled;
        Log.Info($"Startup '{entry.Name}' {(enabled ? "enabled" : "disabled")}");
    }

    /// <summary>Pulls the executable path out of a Run command line.</summary>
    public static string? ExtractExecutable(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        var cmd = Environment.ExpandEnvironmentVariables(command.Trim());
        string path;
        if (cmd.StartsWith('"'))
        {
            var end = cmd.IndexOf('"', 1);
            path = end > 1 ? cmd[1..end] : cmd.Trim('"');
        }
        else
        {
            var idx = cmd.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            path = idx > 0 ? cmd[..(idx + 4)] : cmd.Split(' ')[0];
        }
        return path;
    }

    private static string GetPublisher(string? exe)
    {
        try
        {
            if (exe != null && File.Exists(exe))
                return FileVersionInfo.GetVersionInfo(exe).CompanyName?.Trim() ?? "";
        }
        catch { /* ignore */ }
        return "";
    }
}
