namespace NewKlear.Core.Cleaning;

/// <summary>
/// Defense in depth: refuses to sweep any folder that is (or contains) an important location,
/// no matter what a cleaner definition says.
/// </summary>
public static class SafetyGuard
{
    private static readonly Lazy<HashSet<string>> Protected = new(BuildProtectedSet);

    private static HashSet<string> BuildProtectedSet()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? p)
        {
            if (!string.IsNullOrWhiteSpace(p)) set.Add(Normalize(p));
        }

        foreach (var drive in DriveInfo.GetDrives()) Add(drive.RootDirectory.FullName);

        Environment.SpecialFolder[] folders =
        [
            Environment.SpecialFolder.Windows, Environment.SpecialFolder.System, Environment.SpecialFolder.SystemX86,
            Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86,
            Environment.SpecialFolder.CommonApplicationData, Environment.SpecialFolder.UserProfile,
            Environment.SpecialFolder.ApplicationData, Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.Desktop,
            Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.MyPictures,
            Environment.SpecialFolder.MyMusic, Environment.SpecialFolder.MyVideos,
            Environment.SpecialFolder.CommonDocuments, Environment.SpecialFolder.CommonDesktopDirectory,
            Environment.SpecialFolder.Favorites, Environment.SpecialFolder.StartMenu,
            Environment.SpecialFolder.CommonStartMenu, Environment.SpecialFolder.Fonts,
        ];
        foreach (var f in folders) Add(Environment.GetFolderPath(f));

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Add(Path.Combine(profile, "Downloads"));
        Add(Path.Combine(profile, "OneDrive"));
        Add(Path.GetDirectoryName(profile)); // C:\Users
        Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "WinSxS"));
        return set;
    }

    public static string Normalize(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    /// <summary>True if <paramref name="folder"/> may be swept.</summary>
    public static bool IsSweepAllowed(string folder)
    {
        string full;
        try { full = Normalize(folder); }
        catch { return false; }

        if (full.Length < 4) return false; // drive roots like C:\
        if (Protected.Value.Contains(full)) return false;

        // Refuse a folder that is a parent of any protected folder (e.g. C:\Users\Name\..)
        var withSep = full + Path.DirectorySeparatorChar;
        foreach (var p in Protected.Value)
        {
            if (p.StartsWith(withSep, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }
}
