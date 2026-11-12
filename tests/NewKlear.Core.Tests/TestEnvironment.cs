using NewKlear.Core.Infrastructure;

namespace NewKlear.Core.Tests;

/// <summary>Redirects NewKlear's data folder to a throwaway temp folder for every test run.</summary>
public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NewKlearTests", Guid.NewGuid().ToString("N"));

    public TempDir()
    {
        Directory.CreateDirectory(Path);
        AppPaths.Root = System.IO.Path.Combine(Path, "_appdata");
    }

    public string File(string relative, int bytes = 100, DateTime? lastWrite = null)
    {
        var full = System.IO.Path.Combine(Path, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        System.IO.File.WriteAllBytes(full, new byte[bytes]);
        if (lastWrite.HasValue) System.IO.File.SetLastWriteTime(full, lastWrite.Value);
        return full;
    }

    public void Dispose()
    {
        try { Directory.Delete(Path, true); } catch { /* best effort */ }
    }
}
