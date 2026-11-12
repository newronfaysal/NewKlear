using NewKlear.Core.Infrastructure;

namespace NewKlear.Core;

public sealed class AppSettings
{
    public string ThemeId { get; set; } = "ember";
    public bool IncludeDeveloperCaches { get; set; } = true;
    public bool DeprovisionAppsByDefault { get; set; }
    public bool PromptRestorePointBeforeTweaks { get; set; } = true;
    public DateTime? LastCleanTime { get; set; }
    public long LastCleanBytes { get; set; }
    public long TotalBytesCleaned { get; set; }

    public static AppSettings Load() => JsonStore.Load<AppSettings>(AppPaths.SettingsFile);
    public void Save()
    {
        try { JsonStore.Save(AppPaths.SettingsFile, this); }
        catch (Exception ex) { Log.Error("Could not save settings", ex); }
    }
}
