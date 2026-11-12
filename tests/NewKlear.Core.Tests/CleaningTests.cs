using System.Diagnostics;
using NewKlear.Core.Cleaning;

namespace NewKlear.Core.Tests;

public class SafetyGuardTests
{
    [Theory]
    [InlineData(Environment.SpecialFolder.UserProfile)]
    [InlineData(Environment.SpecialFolder.MyDocuments)]
    [InlineData(Environment.SpecialFolder.Desktop)]
    [InlineData(Environment.SpecialFolder.Windows)]
    [InlineData(Environment.SpecialFolder.System)]
    [InlineData(Environment.SpecialFolder.ProgramFiles)]
    [InlineData(Environment.SpecialFolder.LocalApplicationData)]
    [InlineData(Environment.SpecialFolder.ApplicationData)]
    public void Refuses_protected_folders(Environment.SpecialFolder folder)
    {
        Assert.False(SafetyGuard.IsSweepAllowed(Environment.GetFolderPath(folder)));
    }

    [Fact]
    public void Refuses_drive_roots_and_parents_of_protected_folders()
    {
        Assert.False(SafetyGuard.IsSweepAllowed(@"C:\"));
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.False(SafetyGuard.IsSweepAllowed(Path.GetDirectoryName(profile)!)); // C:\Users
        Assert.False(SafetyGuard.IsSweepAllowed(Path.Combine(profile, "Downloads")));
    }

    [Fact]
    public void Allows_temp_and_cache_folders()
    {
        Assert.True(SafetyGuard.IsSweepAllowed(Path.GetTempPath()));
        Assert.True(SafetyGuard.IsSweepAllowed(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")));
        Assert.True(SafetyGuard.IsSweepAllowed(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache")));
    }

    [Fact]
    public void Every_catalog_directory_target_is_allowed()
    {
        // Guards against a typo in the catalog accidentally pointing at a protected folder.
        foreach (var cleaner in CleanerCatalog.Build(new AppSettings { IncludeDeveloperCaches = true }).OfType<PathCleaner>())
        foreach (var t in cleaner.ResolveTargets().Where(t => !t.IsFile))
            Assert.True(SafetyGuard.IsSweepAllowed(t.Path), $"{cleaner.Id} targets protected folder {t.Path}");
    }
}

public class FileSweeperTests
{
    [Fact]
    public void Scan_counts_files_and_respects_pattern()
    {
        using var tmp = new TempDir();
        tmp.File(@"cache\a.tmp", 1000);
        tmp.File(@"cache\sub\b.tmp", 500);
        tmp.File(@"cache\keep.txt", 200);

        var all = FileSweeper.Scan([new PathTarget(Path.Combine(tmp.Path, "cache"))], CancellationToken.None);
        Assert.Equal(1700, all.Bytes);
        Assert.Equal(3, all.Files);

        var tmpOnly = FileSweeper.Scan([new PathTarget(Path.Combine(tmp.Path, "cache"), "*.tmp")], CancellationToken.None);
        Assert.Equal(1500, tmpOnly.Bytes);
    }

    [Fact]
    public void Delete_removes_files_and_empty_subfolders_but_keeps_root()
    {
        using var tmp = new TempDir();
        var root = Path.Combine(tmp.Path, "cache");
        tmp.File(@"cache\a.bin", 10);
        tmp.File(@"cache\deep\er\b.bin", 10);

        var r = FileSweeper.Delete([new PathTarget(root)], null, CancellationToken.None);

        Assert.Equal(2, r.FilesDeleted);
        Assert.Equal(20, r.BytesFreed);
        Assert.True(Directory.Exists(root));
        Assert.Empty(Directory.EnumerateFileSystemEntries(root));
    }

    [Fact]
    public void MinAge_keeps_recent_files()
    {
        using var tmp = new TempDir();
        var root = Path.Combine(tmp.Path, "temp");
        var old = tmp.File(@"temp\old.tmp", 10, DateTime.Now.AddDays(-2));
        var fresh = tmp.File(@"temp\fresh.tmp", 10);

        FileSweeper.Delete([new PathTarget(root, MinAge: TimeSpan.FromHours(6))], null, CancellationToken.None);

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(fresh));
    }

    [Fact]
    public void Locked_files_are_skipped_not_fatal()
    {
        using var tmp = new TempDir();
        var root = Path.Combine(tmp.Path, "cache");
        var locked = tmp.File(@"cache\locked.bin", 10);
        tmp.File(@"cache\free.bin", 10);

        CleanResult r;
        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
            r = FileSweeper.Delete([new PathTarget(root)], null, CancellationToken.None);

        Assert.Equal(1, r.FilesDeleted);
        Assert.Equal(1, r.FilesSkipped);
        Assert.True(File.Exists(locked));
    }

    [Fact]
    public void Never_follows_junctions_out_of_the_target()
    {
        using var tmp = new TempDir();
        var precious = tmp.File(@"precious\important.docx", 50);
        var cache = Path.Combine(tmp.Path, "cache");
        Directory.CreateDirectory(cache);
        var link = Path.Combine(cache, "link");

        // Directory junction (no admin needed) pointing at the folder we must never touch.
        var mk = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{Path.GetDirectoryName(precious)}\"")
        { CreateNoWindow = true, UseShellExecute = false })!;
        mk.WaitForExit();
        Assert.True(Directory.Exists(link), "junction could not be created");

        var scan = FileSweeper.Scan([new PathTarget(cache)], CancellationToken.None);
        FileSweeper.Delete([new PathTarget(cache)], null, CancellationToken.None);

        Assert.Equal(0, scan.Files);
        Assert.True(File.Exists(precious));
    }

    [Fact]
    public void Protected_target_is_ignored_entirely()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var scan = FileSweeper.Scan([new PathTarget(docs)], CancellationToken.None);
        Assert.Equal(0, scan.Files);
    }
}
