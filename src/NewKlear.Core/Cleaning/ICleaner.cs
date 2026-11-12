namespace NewKlear.Core.Cleaning;

public enum Risk
{
    Safe,
    Caution,
}

public sealed record ScanResult(long Bytes, int Files, string? Note = null)
{
    public static readonly ScanResult Empty = new(0, 0);
    /// <summary>Use when size can only be known after running (e.g. DISM).</summary>
    public static ScanResult Unknown(string note) => new(-1, 0, note);
}

public sealed record CleanResult(long BytesFreed, int FilesDeleted, int FilesSkipped, string? Note = null);

public interface ICleaner
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    string Category { get; }
    Risk Risk { get; }
    bool DefaultSelected { get; }
    /// <summary>Shown to the user before cleaning, e.g. "Removes the ability to roll back".</summary>
    string? Warning { get; }
    bool RequiresAdmin { get; }

    Task<ScanResult> ScanAsync(CancellationToken ct);
    Task<CleanResult> CleanAsync(IProgress<string>? progress, CancellationToken ct);
}

public abstract class CleanerBase : ICleaner
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public Risk Risk { get; init; } = Risk.Safe;
    public bool DefaultSelected { get; init; } = true;
    public string? Warning { get; init; }
    public bool RequiresAdmin { get; init; }

    public abstract Task<ScanResult> ScanAsync(CancellationToken ct);
    public abstract Task<CleanResult> CleanAsync(IProgress<string>? progress, CancellationToken ct);
}

public static class CleanerCategories
{
    public const string Windows = "Windows";
    public const string SystemCaches = "System caches";
    public const string Browsers = "Browsers";
    public const string Apps = "Applications";
    public const string Developer = "Developer";
    public const string Advanced = "Advanced (use with care)";

    public static readonly string[] Order = [Windows, SystemCaches, Browsers, Apps, Developer, Advanced];
}
