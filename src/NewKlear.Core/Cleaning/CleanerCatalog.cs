using NewKlear.Core.Infrastructure;

namespace NewKlear.Core.Cleaning;

/// <summary>All cleaning items NewKlear offers. Paths only — never user documents.</summary>
public static class CleanerCatalog
{
    private static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static string Roaming => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static string ProgramData => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    private static string WinDir => Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    private static string SystemDrive => Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";

    private static readonly TimeSpan TempMinAge = TimeSpan.FromHours(6);

    public static IReadOnlyList<ICleaner> Build(AppSettings settings)
    {
        var list = new List<ICleaner>();
        list.AddRange(WindowsCleaners());
        list.AddRange(SystemCacheCleaners());
        list.AddRange(BrowserCleaners());
        list.AddRange(AppCleaners());
        if (settings.IncludeDeveloperCaches) list.AddRange(DeveloperCleaners());
        list.AddRange(AdvancedCleaners());
        return list;
    }

    // ---------------------------------------------------------------- Windows
    private static IEnumerable<ICleaner> WindowsCleaners()
    {
        yield return new PathCleaner(new PathTarget(Path.GetTempPath(), MinAge: TempMinAge))
        {
            Id = "win.usertemp", Name = "User temporary files", Category = CleanerCategories.Windows,
            Description = "Leftovers from installers and apps in your %TEMP% folder. Files newer than 6 hours are kept.",
        };
        yield return new PathCleaner(new PathTarget(Path.Combine(WinDir, "Temp"), MinAge: TempMinAge))
        {
            Id = "win.systemtemp", Name = "Windows temporary files", Category = CleanerCategories.Windows,
            Description = "Temporary files in C:\\Windows\\Temp. Files newer than 6 hours are kept.", RequiresAdmin = true,
        };
        yield return new RecycleBinCleaner
        {
            Id = "win.recyclebin", Name = "Recycle Bin", Category = CleanerCategories.Windows,
            Description = "Permanently deletes everything in the Recycle Bin on all drives.",
        };
        yield return new PathCleaner(new PathTarget(Path.Combine(WinDir, @"SoftwareDistribution\Download")))
        {
            Id = "win.updatecache", Name = "Windows Update download cache", Category = CleanerCategories.Windows,
            Description = "Update packages that were already installed. Windows re-downloads anything it still needs.",
            StopServices = ["wuauserv", "bits"], RequiresAdmin = true,
        };
        yield return new PathCleaner(new PathTarget(Path.Combine(WinDir,
            @"ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache")))
        {
            Id = "win.deliveryopt", Name = "Delivery Optimization cache", Category = CleanerCategories.Windows,
            Description = "Update and Store files cached for sharing with other PCs.",
            StopServices = ["DoSvc"], RequiresAdmin = true,
        };
        yield return new PathCleaner(
            new PathTarget(Path.Combine(ProgramData, @"Microsoft\Windows\WER\ReportArchive")),
            new PathTarget(Path.Combine(ProgramData, @"Microsoft\Windows\WER\ReportQueue")),
            new PathTarget(Path.Combine(ProgramData, @"Microsoft\Windows\WER\Temp")),
            new PathTarget(Path.Combine(Local, @"Microsoft\Windows\WER")),
            new PathTarget(Path.Combine(Local, "CrashDumps")),
            new PathTarget(Path.Combine(WinDir, "Minidump")),
            new PathTarget(Path.Combine(WinDir, "LiveKernelReports"), "*.dmp"),
            PathTarget.File(Path.Combine(WinDir, "MEMORY.DMP")))
        {
            Id = "win.errorreports", Name = "Error reports & crash dumps", Category = CleanerCategories.Windows,
            Description = "Windows Error Reporting files and memory dumps from past crashes.", RequiresAdmin = true,
        };
        yield return new PathCleaner(
            new PathTarget(Path.Combine(WinDir, @"Logs\CBS"), "*.log"),
            new PathTarget(Path.Combine(WinDir, @"Logs\CBS"), "*.cab"),
            new PathTarget(Path.Combine(WinDir, @"Logs\DISM"), "*.log"),
            new PathTarget(Path.Combine(WinDir, @"Logs\MoSetup"), "*.log"),
            new PathTarget(Path.Combine(WinDir, @"Logs\WindowsUpdate"), "*.etl"),
            new PathTarget(Path.Combine(WinDir, "Panther"), "*.log", Recursive: false),
            new PathTarget(Path.Combine(WinDir, @"Logs\SIH"), "*.etl"),
            new PathTarget(Path.Combine(WinDir, @"Logs\NetSetup"), "*.etl"))
        {
            Id = "win.logs", Name = "Windows log files", Category = CleanerCategories.Windows,
            Description = "Setup, servicing and update logs. Files in use are skipped.", RequiresAdmin = true,
        };
    }

    // ---------------------------------------------------------- System caches
    private static IEnumerable<ICleaner> SystemCacheCleaners()
    {
        var explorer = Path.Combine(Local, @"Microsoft\Windows\Explorer");
        yield return new PathCleaner(
            new PathTarget(explorer, "thumbcache_*.db", Recursive: false, RemoveEmptyDirs: false),
            new PathTarget(explorer, "iconcache_*.db", Recursive: false, RemoveEmptyDirs: false),
            PathTarget.File(Path.Combine(Local, "IconCache.db")))
        {
            Id = "sys.thumbnails", Name = "Thumbnail & icon cache", Category = CleanerCategories.SystemCaches,
            Description = "Rebuilt automatically. Fixes wrong or blank icons. Explorer restarts briefly.",
            RestartsExplorer = true,
        };
        yield return new PathCleaner(
            new PathTarget(Path.Combine(Local, "D3DSCache")),
            new PathTarget(Path.Combine(Local, @"NVIDIA\DXCache")),
            new PathTarget(Path.Combine(Local, @"NVIDIA\GLCache")),
            new PathTarget(Path.Combine(Local, @"AMD\DxCache")),
            new PathTarget(Path.Combine(Local, @"AMD\DxcCache")),
            new PathTarget(Path.Combine(Local, @"AMD\GLCache")),
            new PathTarget(Path.Combine(Local, @"Intel\ShaderCache")))
        {
            Id = "sys.shadercache", Name = "DirectX & GPU shader cache", Category = CleanerCategories.SystemCaches,
            Description = "Compiled shaders. Games rebuild them, so the first launch afterwards may stutter. Clear it if games show glitches or after a GPU driver update.",
            DefaultSelected = false,
        };
        yield return new PathCleaner(
            new PathTarget(Path.Combine(WinDir, @"ServiceProfiles\LocalService\AppData\Local\FontCache"), "*FontCache*"))
        {
            Id = "sys.fontcache", Name = "Font cache", Category = CleanerCategories.SystemCaches,
            Description = "Rebuilt automatically. Fixes garbled or missing fonts.",
            StopServices = ["FontCache"], RequiresAdmin = true, DefaultSelected = false,
        };
        yield return new PathCleaner(
            new PathTarget(Path.Combine(Local, @"Packages\Microsoft.WindowsStore_8wekyb3d8bbwe\LocalCache")),
            new PathTarget(Path.Combine(Local, @"Packages\Microsoft.WindowsStore_8wekyb3d8bbwe\AC\INetCache")))
        {
            Id = "sys.storecache", Name = "Microsoft Store cache", Category = CleanerCategories.SystemCaches,
            Description = "Cached Store pages and images.",
        };
        yield return new PathCleaner(
            new PathTarget(Path.Combine(Local, @"Microsoft\Windows\INetCache"), RemoveEmptyDirs: false),
            new PathTarget(Path.Combine(Local, @"Microsoft\Windows\WebCache"), "*.log", Recursive: false, RemoveEmptyDirs: false))
        {
            Id = "sys.inetcache", Name = "Windows internet cache", Category = CleanerCategories.SystemCaches,
            Description = "Web cache used by Windows components and older apps.",
        };
    }

    // --------------------------------------------------------------- Browsers
    private static IEnumerable<PathTarget> ChromiumCaches(string userDataDir)
    {
        if (!Directory.Exists(userDataDir)) yield break;
        string[] profileCaches = ["Cache", "Code Cache", "GPUCache", @"Service Worker\CacheStorage", @"Service Worker\ScriptCache"];
        foreach (var profile in Directory.EnumerateDirectories(userDataDir))
        {
            var name = Path.GetFileName(profile);
            if (name != "Default" && !name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase) && name != "Guest Profile")
                continue;
            foreach (var c in profileCaches) yield return new PathTarget(Path.Combine(profile, c));
        }
        foreach (var c in new[] { "ShaderCache", "GrShaderCache", "GraphiteDawnCache" })
            yield return new PathTarget(Path.Combine(userDataDir, c));
    }

    private static IEnumerable<ICleaner> BrowserCleaners()
    {
        const string note = " Only the cache is removed — cookies, passwords, history and logins are kept. Close the browser for best results.";
        yield return new PathCleaner(() => ChromiumCaches(Path.Combine(Local, @"Microsoft\Edge\User Data")))
        { Id = "br.edge", Name = "Microsoft Edge cache", Category = CleanerCategories.Browsers, Description = "Cached web pages and images." + note };
        yield return new PathCleaner(() => ChromiumCaches(Path.Combine(Local, @"Google\Chrome\User Data")))
        { Id = "br.chrome", Name = "Google Chrome cache", Category = CleanerCategories.Browsers, Description = "Cached web pages and images." + note };
        yield return new PathCleaner(() => ChromiumCaches(Path.Combine(Local, @"BraveSoftware\Brave-Browser\User Data")))
        { Id = "br.brave", Name = "Brave cache", Category = CleanerCategories.Browsers, Description = "Cached web pages and images." + note };
        yield return new PathCleaner(() => ChromiumCaches(Path.Combine(Roaming, @"Opera Software\Opera Stable")).Concat(
            [new PathTarget(Path.Combine(Local, @"Opera Software\Opera Stable\Cache"))]))
        { Id = "br.opera", Name = "Opera cache", Category = CleanerCategories.Browsers, Description = "Cached web pages and images." + note };
        yield return new PathCleaner(FirefoxCaches)
        { Id = "br.firefox", Name = "Mozilla Firefox cache", Category = CleanerCategories.Browsers, Description = "Cached web pages and images." + note };
    }

    private static IEnumerable<PathTarget> FirefoxCaches()
    {
        var profiles = Path.Combine(Local, @"Mozilla\Firefox\Profiles");
        if (!Directory.Exists(profiles)) yield break;
        foreach (var p in Directory.EnumerateDirectories(profiles))
        {
            yield return new PathTarget(Path.Combine(p, "cache2"));
            yield return new PathTarget(Path.Combine(p, "startupCache"));
            yield return new PathTarget(Path.Combine(p, "thumbnails"));
        }
    }

    // ------------------------------------------------------------------- Apps
    private static IEnumerable<PathTarget> ElectronCaches(string root) =>
        new[] { "Cache", "Code Cache", "GPUCache", "DawnCache", "DawnGraphiteCache", "DawnWebGPUCache" }
            .Select(c => new PathTarget(Path.Combine(root, c)));

    private static IEnumerable<ICleaner> AppCleaners()
    {
        yield return new PathCleaner(() => ElectronCaches(Path.Combine(Roaming, @"Microsoft\Teams"))
            .Concat(ChromiumCaches(Path.Combine(Local, @"Packages\MSTeams_8wekyb3d8bbwe\LocalCache\Microsoft\MSTeams\EBWebView"))))
        { Id = "app.teams", Name = "Microsoft Teams cache", Category = CleanerCategories.Apps, Description = "Cached chats media and web content. You stay signed in." };
        yield return new PathCleaner(() => ElectronCaches(Path.Combine(Roaming, "discord")))
        { Id = "app.discord", Name = "Discord cache", Category = CleanerCategories.Apps, Description = "Cached images, videos and code. You stay signed in." };
        yield return new PathCleaner(
            new PathTarget(Path.Combine(Local, @"Spotify\Storage")),
            new PathTarget(Path.Combine(Local, @"Spotify\Data")),
            new PathTarget(Path.Combine(Local, @"Packages\SpotifyAB.SpotifyMusic_zpdnekdrzrea0\LocalCache\Spotify\Data")))
        { Id = "app.spotify", Name = "Spotify cache", Category = CleanerCategories.Apps, Description = "Streamed music cache. Downloaded songs for offline use are not touched." };
        yield return new PathCleaner(
            new PathTarget(Path.Combine(Roaming, @"Adobe\Common\Media Cache Files")),
            new PathTarget(Path.Combine(Roaming, @"Adobe\Common\Media Cache")),
            new PathTarget(Path.Combine(Roaming, @"Adobe\Common\Peak Files")))
        { Id = "app.adobe", Name = "Adobe media cache", Category = CleanerCategories.Apps, Description = "Premiere / After Effects media cache and peak files. Rebuilt when projects open." };
        yield return new PathCleaner(() => ElectronCaches(Path.Combine(Roaming, "Slack")))
        { Id = "app.slack", Name = "Slack cache", Category = CleanerCategories.Apps, Description = "Cached images and web content. You stay signed in." };
        yield return new PathCleaner(
            new PathTarget(Path.Combine(Local, @"Steam\htmlcache")),
            new PathTarget(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Steam\appcache\httpcache")),
            new PathTarget(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Steam\logs"), "*.txt"))
        { Id = "app.steam", Name = "Steam web cache & logs", Category = CleanerCategories.Apps, Description = "Store page cache and client logs. Games are not touched." };
    }

    // -------------------------------------------------------------- Developer
    private static IEnumerable<ICleaner> DeveloperCleaners()
    {
        yield return new PathCleaner(new PathTarget(Path.Combine(Local, @"npm-cache\_cacache")), new PathTarget(Path.Combine(Local, @"npm-cache\_logs")))
        { Id = "dev.npm", Name = "npm cache", Category = CleanerCategories.Developer, Description = "Downloaded npm packages. Re-downloaded on the next install." };
        yield return new PathCleaner(new PathTarget(Path.Combine(Local, @"Yarn\Cache")), new PathTarget(Path.Combine(Local, @"pnpm-cache")))
        { Id = "dev.yarn", Name = "Yarn / pnpm cache", Category = CleanerCategories.Developer, Description = "Downloaded packages. Re-downloaded on the next install." };
        yield return new PathCleaner(new PathTarget(Path.Combine(Local, @"pip\Cache")), new PathTarget(Path.Combine(Local, @"pip\cache")))
        { Id = "dev.pip", Name = "pip cache", Category = CleanerCategories.Developer, Description = "Downloaded Python wheels. Re-downloaded on the next install." };
        yield return new PathCleaner(new PathTarget(Path.Combine(Local, @"NuGet\v3-cache")), new PathTarget(Path.Combine(Local, @"NuGet\http-cache")), new PathTarget(Path.Combine(Local, @"NuGet\plugins-cache")))
        { Id = "dev.nuget", Name = "NuGet HTTP cache", Category = CleanerCategories.Developer, Description = "NuGet download cache. Your global packages folder is not touched." };
        yield return new PathCleaner(() => ElectronCaches(Path.Combine(Roaming, "Code")).Concat(
            [new PathTarget(Path.Combine(Roaming, @"Code\CachedData")), new PathTarget(Path.Combine(Roaming, @"Code\CachedExtensionVSIXs")),
             new PathTarget(Path.Combine(Roaming, @"Code\logs"))]))
        { Id = "dev.vscode", Name = "VS Code cache & logs", Category = CleanerCategories.Developer, Description = "Editor caches, old update data and logs. Settings and extensions are kept." };
    }

    // --------------------------------------------------------------- Advanced
    private static long DirSize(string path) =>
        FileSweeper.Scan([new PathTarget(path, RemoveEmptyDirs: false)], CancellationToken.None).Bytes;

    private static IEnumerable<ICleaner> AdvancedCleaners()
    {
        var windowsOld = Path.Combine(SystemDrive, "Windows.old");
        yield return new CommandCleaner
        {
            Id = "adv.windowsold", Name = "Previous Windows installation (Windows.old)", Category = CleanerCategories.Advanced,
            Description = "Files kept after a Windows feature update.", Risk = Risk.Caution, DefaultSelected = false, RequiresAdmin = true,
            Warning = "You will no longer be able to go back to the previous version of Windows.",
            Scan = ct => Task.Run(() => Directory.Exists(windowsOld) ? new ScanResult(DirSize(windowsOld), 0) : ScanResult.Empty, ct),
            Run = async (p, ct) =>
            {
                p?.Report("Removing previous Windows installation (Disk Cleanup)…");
                return await DiskCleanupHelper.RunAsync(["Previous Installations", "Windows Upgrade Log Files"], ct);
            },
        };
        yield return new CommandCleaner
        {
            Id = "adv.componentstore", Name = "Windows component store cleanup (DISM)", Category = CleanerCategories.Advanced,
            Description = "Removes superseded versions of Windows components left behind by updates. Can take 5–20 minutes.",
            Risk = Risk.Caution, DefaultSelected = false, RequiresAdmin = true,
            Warning = "Some already-installed updates may no longer be uninstallable.",
            Scan = _ => Task.FromResult(ScanResult.Unknown("Size known after cleanup")),
            Run = async (p, ct) =>
            {
                p?.Report("Running DISM component cleanup (this can take a while)…");
                var r = await ProcessRunner.RunAsync(ProcessRunner.System32("dism.exe"), "/Online /Cleanup-Image /StartComponentCleanup /NoRestart", ct, TimeSpan.FromMinutes(60));
                return r.Success ? null : $"DISM exited with code {r.ExitCode}";
            },
        };
        var hiberfil = Path.Combine(SystemDrive, "hiberfil.sys");
        yield return new CommandCleaner
        {
            Id = "adv.hibernation", Name = "Hibernation file (hiberfil.sys)", Category = CleanerCategories.Advanced,
            Description = "Turns off hibernation to reclaim the space it reserves (often several GB).",
            Risk = Risk.Caution, DefaultSelected = false, RequiresAdmin = true,
            Warning = "Hibernate and Fast Startup will be disabled. Re-enable any time with 'powercfg /h on'.",
            Scan = _ => Task.Run(() =>
            {
                try { var fi = new FileInfo(hiberfil); return fi.Exists ? new ScanResult(fi.Length, 1) : ScanResult.Empty; }
                catch { return ScanResult.Empty; }
            }),
            Run = async (p, ct) =>
            {
                p?.Report("Turning off hibernation…");
                var r = await ProcessRunner.RunAsync(ProcessRunner.System32("powercfg.exe"), "/hibernate off", ct);
                return r.Success ? null : r.StdErr.Trim();
            },
        };
        var evtx = Path.Combine(Environment.SystemDirectory, @"winevt\Logs");
        yield return new CommandCleaner
        {
            Id = "adv.eventlogs", Name = "Windows event logs", Category = CleanerCategories.Advanced,
            Description = "Clears all Event Viewer logs.", Risk = Risk.Caution, DefaultSelected = false, RequiresAdmin = true,
            Warning = "Past events will no longer be available for troubleshooting.",
            Scan = ct => Task.Run(() => new ScanResult(DirSize(evtx), 0), ct),
            Run = async (p, ct) =>
            {
                var wevtutil = ProcessRunner.System32("wevtutil.exe");
                var list = await ProcessRunner.RunAsync(wevtutil, "el", ct);
                var logs = list.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var failed = 0;
                foreach (var log in logs)
                {
                    ct.ThrowIfCancellationRequested();
                    p?.Report($"Clearing {log}");
                    var r = await ProcessRunner.RunAsync(wevtutil, $"cl \"{log}\"", ct);
                    if (!r.Success) failed++;
                }
                return failed > 0 ? $"{logs.Length - failed} of {logs.Length} logs cleared" : null;
            },
        };
    }
}
