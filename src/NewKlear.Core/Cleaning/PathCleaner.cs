using NewKlear.Core.Infrastructure;

namespace NewKlear.Core.Cleaning;

/// <summary>Cleaner that sweeps a set of folders/files, optionally stopping services around the sweep.</summary>
public sealed class PathCleaner : CleanerBase
{
    private readonly Func<IEnumerable<PathTarget>> _targets;

    public PathCleaner(Func<IEnumerable<PathTarget>> targets) => _targets = targets;

    public PathCleaner(params PathTarget[] targets) : this(() => targets) { }

    /// <summary>Services stopped before cleaning and restarted afterwards if they were running.</summary>
    public string[] StopServices { get; init; } = [];

    /// <summary>Restart Explorer after cleaning (needed for icon/thumbnail caches it keeps open).</summary>
    public bool RestartsExplorer { get; init; }

    public IEnumerable<PathTarget> ResolveTargets()
    {
        try { return _targets().ToList(); }
        catch (Exception ex)
        {
            Log.Warn($"{Name}: could not resolve targets: {ex.Message}");
            return [];
        }
    }

    public override Task<ScanResult> ScanAsync(CancellationToken ct) =>
        Task.Run(() => FileSweeper.Scan(ResolveTargets(), ct), ct);

    public override async Task<CleanResult> CleanAsync(IProgress<string>? progress, CancellationToken ct)
    {
        Log.Info($"Cleaning: {Name}");
        var restart = new List<string>();
        foreach (var s in StopServices)
            if (await ServiceHelper.StopAsync(s)) restart.Add(s);

        if (RestartsExplorer)
        {
            // Kill explorer first so it releases the cache files, then sweep, then bring it back.
            var session = System.Diagnostics.Process.GetCurrentProcess().SessionId;
            foreach (var p in System.Diagnostics.Process.GetProcessesByName("explorer").Where(p => p.SessionId == session))
            {
                try { p.Kill(); p.WaitForExit(5000); } catch { /* ignore */ }
            }
            await Task.Delay(800, CancellationToken.None);
        }

        try
        {
            return await Task.Run(() => FileSweeper.Delete(ResolveTargets(), progress, ct), ct);
        }
        finally
        {
            foreach (var s in restart) await ServiceHelper.StartAsync(s);
            if (RestartsExplorer) await ShellHelper.RestartExplorerAsync();
        }
    }
}
