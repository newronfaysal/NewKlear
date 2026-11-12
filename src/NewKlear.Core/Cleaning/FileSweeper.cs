using NewKlear.Core.Infrastructure;

namespace NewKlear.Core.Cleaning;

/// <summary>A folder (or single file) a cleaner sweeps.</summary>
public sealed record PathTarget(string Path, string Pattern = "*", bool Recursive = true, bool RemoveEmptyDirs = true,
    TimeSpan MinAge = default, bool IsFile = false)
{
    public static PathTarget File(string path) => new(path, IsFile: true, Recursive: false, RemoveEmptyDirs: false);
}

/// <summary>
/// Enumerates and deletes files safely: never follows junctions/symlinks, skips locked files,
/// honours a minimum age and never sweeps protected folders.
/// </summary>
public static class FileSweeper
{
    public static IEnumerable<FileInfo> Enumerate(PathTarget target, CancellationToken ct)
    {
        if (target.IsFile)
        {
            FileInfo? single = null;
            try
            {
                var fi = new FileInfo(target.Path);
                if (fi.Exists) single = fi;
            }
            catch { /* inaccessible */ }
            if (single != null) yield return single;
            yield break;
        }

        if (!Directory.Exists(target.Path) || !SafetyGuard.IsSweepAllowed(target.Path)) yield break;

        var cutoff = target.MinAge > TimeSpan.Zero ? DateTime.Now - target.MinAge : DateTime.MaxValue;
        var stack = new Stack<DirectoryInfo>();
        stack.Push(new DirectoryInfo(target.Path));

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var dir = stack.Pop();

            FileInfo[] files;
            try { files = dir.GetFiles(target.Pattern); }
            catch { continue; }

            foreach (var f in files)
            {
                if ((f.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                if (cutoff != DateTime.MaxValue && f.LastWriteTime > cutoff) continue;
                yield return f;
            }

            if (!target.Recursive) continue;
            DirectoryInfo[] subs;
            try { subs = dir.GetDirectories(); }
            catch { continue; }
            foreach (var s in subs)
            {
                if ((s.Attributes & FileAttributes.ReparsePoint) != 0) continue; // never follow junctions
                stack.Push(s);
            }
        }
    }

    public static ScanResult Scan(IEnumerable<PathTarget> targets, CancellationToken ct)
    {
        long bytes = 0;
        var count = 0;
        foreach (var t in targets)
        foreach (var f in Enumerate(t, ct))
        {
            try { bytes += f.Length; count++; }
            catch { /* vanished */ }
        }
        return new ScanResult(bytes, count);
    }

    public static CleanResult Delete(IEnumerable<PathTarget> targets, IProgress<string>? progress, CancellationToken ct)
    {
        long freed = 0;
        int deleted = 0, skipped = 0;

        foreach (var t in targets)
        {
            progress?.Report(t.Path);
            foreach (var f in Enumerate(t, ct).ToList())
            {
                ct.ThrowIfCancellationRequested();
                long len = 0;
                try
                {
                    len = f.Length;
                    if (f.IsReadOnly) f.IsReadOnly = false;
                    f.Delete();
                    freed += len;
                    deleted++;
                }
                catch
                {
                    skipped++; // locked or access denied — leave it alone
                }
            }

            if (t.RemoveEmptyDirs && !t.IsFile && Directory.Exists(t.Path) && SafetyGuard.IsSweepAllowed(t.Path))
                RemoveEmptySubdirectories(new DirectoryInfo(t.Path));
        }

        Log.Info($"Swept {deleted} files ({Format.Bytes(freed)}), skipped {skipped} in use/protected");
        return new CleanResult(freed, deleted, skipped);
    }

    /// <summary>Deletes empty sub-folders bottom-up. Never deletes the root itself and never touches junctions.</summary>
    private static void RemoveEmptySubdirectories(DirectoryInfo root)
    {
        DirectoryInfo[] subs;
        try { subs = root.GetDirectories(); }
        catch { return; }

        foreach (var s in subs)
        {
            if ((s.Attributes & FileAttributes.ReparsePoint) != 0) continue;
            RemoveEmptySubdirectories(s);
            try
            {
                if (!s.EnumerateFileSystemInfos().Any()) s.Delete(false);
            }
            catch { /* in use */ }
        }
    }
}
