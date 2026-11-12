namespace NewKlear.Core.Infrastructure;

/// <summary>Locations where NewKlear stores its own data. Everything lives under %LocalAppData%\NewKlear.</summary>
public static class AppPaths
{
    public static string Root { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NewKlear");

    public static string Logs => Ensure(Path.Combine(Root, "logs"));
    public static string Backups => Ensure(Path.Combine(Root, "backups"));
    public static string SettingsFile => Path.Combine(Ensure(Root), "settings.json");
    public static string HistoryFile => Path.Combine(Ensure(Root), "history.json");
    public static string TweakBackupFile => Path.Combine(Backups, "tweak-state.json");

    private static string Ensure(string dir)
    {
        Directory.CreateDirectory(dir);
        return dir;
    }
}
