using NewKlear.Core.Infrastructure;

namespace NewKlear.Core.History;

public enum HistoryKind
{
    Cleaned,
    TweakApplied,
    TweakReverted,
    AppRemoved,
    AppRestored,
    StartupChanged,
    PowerPlanChanged,
    RestorePoint,
}

public sealed class HistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Time { get; set; } = DateTime.Now;
    public HistoryKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    /// <summary>Tweak id, package name, or power plan GUID — whatever is needed to undo.</summary>
    public string? Reference { get; set; }
    public string? Reference2 { get; set; }
    public bool Undone { get; set; }

    public bool CanUndo => !Undone && Kind is HistoryKind.TweakApplied or HistoryKind.AppRemoved or HistoryKind.PowerPlanChanged;
}

public static class HistoryStore
{
    private const int MaxEntries = 500;
    private static readonly object Gate = new();

    public static event Action? Changed;

    public static List<HistoryEntry> Load()
    {
        lock (Gate) return JsonStore.Load<List<HistoryEntry>>(AppPaths.HistoryFile);
    }

    public static void Add(HistoryKind kind, string title, string detail = "", string? reference = null, string? reference2 = null)
    {
        lock (Gate)
        {
            var list = JsonStore.Load<List<HistoryEntry>>(AppPaths.HistoryFile);
            list.Insert(0, new HistoryEntry { Kind = kind, Title = title, Detail = detail, Reference = reference, Reference2 = reference2 });
            if (list.Count > MaxEntries) list.RemoveRange(MaxEntries, list.Count - MaxEntries);
            Save(list);
        }
        Changed?.Invoke();
    }

    public static void MarkUndone(Guid id)
    {
        lock (Gate)
        {
            var list = JsonStore.Load<List<HistoryEntry>>(AppPaths.HistoryFile);
            var e = list.FirstOrDefault(x => x.Id == id);
            if (e is null) return;
            e.Undone = true;
            Save(list);
        }
        Changed?.Invoke();
    }

    /// <summary>When a tweak is reverted from the Optimize page, older "applied" entries are no longer undoable.</summary>
    public static void MarkTweakEntriesUndone(string tweakId)
    {
        lock (Gate)
        {
            var list = JsonStore.Load<List<HistoryEntry>>(AppPaths.HistoryFile);
            foreach (var e in list.Where(e => e.Kind == HistoryKind.TweakApplied && e.Reference == tweakId)) e.Undone = true;
            Save(list);
        }
    }

    private static void Save(List<HistoryEntry> list)
    {
        try { JsonStore.Save(AppPaths.HistoryFile, list); }
        catch (Exception ex) { Log.Error("Could not save history", ex); }
    }
}
