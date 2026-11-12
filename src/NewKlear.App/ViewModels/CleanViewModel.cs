using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewKlear.App.Services;
using NewKlear.Core.Cleaning;
using NewKlear.Core.History;
using NewKlear.Core.Infrastructure;

namespace NewKlear.App.ViewModels;

public partial class CleanerItemViewModel : ObservableObject
{
    public ICleaner Cleaner { get; }
    public string Name => Cleaner.Name;
    public string Description => Cleaner.Description;
    public bool IsCaution => Cleaner.Risk == Risk.Caution;
    public string? Warning => Cleaner.Warning;
    /// <summary>False when the item needs admin rights and NewKlear isn't elevated.</summary>
    public bool IsAvailable { get; }

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private long? _sizeBytes;
    [ObservableProperty] private string? _sizeNote;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _resultText;

    public string SizeText => !IsAvailable ? "Needs admin"
        : SizeBytes is null ? (SizeNote ?? "—")
        : SizeBytes < 0 ? (SizeNote ?? "Unknown")
        : Format.Bytes(SizeBytes.Value);

    public bool HasSize => SizeBytes > 0;

    public CleanerItemViewModel(ICleaner cleaner, bool isAdmin)
    {
        Cleaner = cleaner;
        IsAvailable = !cleaner.RequiresAdmin || isAdmin;
        _isSelected = IsAvailable && cleaner.DefaultSelected;
    }

    partial void OnSizeBytesChanged(long? value) { OnPropertyChanged(nameof(SizeText)); OnPropertyChanged(nameof(HasSize)); }
    partial void OnSizeNoteChanged(string? value) => OnPropertyChanged(nameof(SizeText));
}

public partial class CleanerGroupViewModel : ObservableObject
{
    public string Name { get; }
    public ObservableCollection<CleanerItemViewModel> Items { get; }
    [ObservableProperty] private bool _isExpanded = true;

    public CleanerGroupViewModel(string name, IEnumerable<CleanerItemViewModel> items)
    {
        Name = name;
        Items = new ObservableCollection<CleanerItemViewModel>(items);
        foreach (var i in Items) i.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(CleanerItemViewModel.SizeBytes) or nameof(CleanerItemViewModel.IsSelected))
            {
                OnPropertyChanged(nameof(TotalText));
                OnPropertyChanged(nameof(AllSelected));
            }
        };
    }

    public string TotalText
    {
        get
        {
            var total = Items.Where(i => i.SizeBytes > 0).Sum(i => i.SizeBytes!.Value);
            return total > 0 ? Format.Bytes(total) : "";
        }
    }

    public bool? AllSelected
    {
        get
        {
            var avail = Items.Where(i => i.IsAvailable).ToList();
            if (avail.Count == 0) return false;
            if (avail.All(i => i.IsSelected)) return true;
            return avail.Any(i => i.IsSelected) ? null : false;
        }
        set
        {
            var target = value ?? false;
            foreach (var i in Items.Where(i => i.IsAvailable)) i.IsSelected = target;
        }
    }
}

public partial class CleanViewModel : PageViewModel
{
    private readonly bool _isAdmin = Elevation.IsAdministrator;
    private CancellationTokenSource? _cts;

    public ObservableCollection<CleanerGroupViewModel> Groups { get; } = [];

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _statusText = "Press Scan to find junk files. Nothing is deleted until you press Clean.";
    [ObservableProperty] private bool _hasScanned;
    [ObservableProperty] private string? _lastResult;

    public CleanViewModel() => Rebuild();

    public IEnumerable<CleanerItemViewModel> AllItems => Groups.SelectMany(g => g.Items);

    public long SelectedBytes => AllItems.Where(i => i.IsSelected && i.SizeBytes > 0).Sum(i => i.SizeBytes!.Value);
    public string SelectedBytesText => Format.Bytes(SelectedBytes);
    public int SelectedCount => AllItems.Count(i => i.IsSelected);
    public long TotalFoundBytes => AllItems.Where(i => i.SizeBytes > 0).Sum(i => i.SizeBytes!.Value);

    /// <summary>Rebuilds the list (e.g. after Developer caches is switched on or off in Settings).</summary>
    public void Rebuild()
    {
        Groups.Clear();
        var items = CleanerCatalog.Build(App.Settings).Select(c => new CleanerItemViewModel(c, _isAdmin)).ToList();
        foreach (var cat in CleanerCategories.Order)
        {
            var groupItems = items.Where(i => i.Cleaner.Category == cat).ToList();
            if (groupItems.Count == 0) continue;
            var group = new CleanerGroupViewModel(cat, groupItems) { IsExpanded = cat != CleanerCategories.Advanced };
            group.PropertyChanged += GroupChanged;
            Groups.Add(group);
        }
        HasScanned = false;
        RaiseTotals();
    }

    private void GroupChanged(object? sender, PropertyChangedEventArgs e) => RaiseTotals();

    private void RaiseTotals()
    {
        OnPropertyChanged(nameof(SelectedBytes));
        OnPropertyChanged(nameof(SelectedBytesText));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(TotalFoundBytes));
        CleanCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanScan))]
    public async Task ScanAsync()
    {
        if (IsBusy) return;
        IsBusy = IsScanning = true;
        _cts = new CancellationTokenSource();
        LastResult = null;
        var items = AllItems.Where(i => i.IsAvailable).ToList();
        var done = 0;
        Progress = 0;
        StatusText = "Scanning…";
        using var gate = new SemaphoreSlim(4);

        async Task ScanOne(CleanerItemViewModel item)
        {
            await gate.WaitAsync(_cts.Token);
            try
            {
                item.IsBusy = true;
                item.ResultText = null;
                var r = await item.Cleaner.ScanAsync(_cts.Token);
                item.SizeBytes = r.Bytes;
                item.SizeNote = r.Note;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log.Warn($"Scan failed for {item.Name}: {ex.Message}");
                item.SizeBytes = 0;
            }
            finally
            {
                item.IsBusy = false;
                gate.Release();
                Progress = 100.0 * Interlocked.Increment(ref done) / items.Count;
                StatusText = $"Scanning… {done} of {items.Count}";
            }
        }

        try
        {
            await Task.WhenAll(items.Select(ScanOne));
            HasScanned = true;
            StatusText = $"Found {Format.Bytes(TotalFoundBytes)} of junk. {Format.Bytes(SelectedBytes)} selected.";
            Log.Info($"Scan complete: {Format.Bytes(TotalFoundBytes)} found");
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan cancelled.";
        }
        finally
        {
            IsBusy = IsScanning = false;
            RaiseTotals();
        }
    }

    private bool CanScan() => !IsBusy;
    private bool CanClean() => !IsBusy && SelectedCount > 0;

    partial void OnIsBusyChanged(bool value)
    {
        ScanCommand.NotifyCanExecuteChanged();
        CleanCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanClean))]
    private async Task CleanAsync()
    {
        // After a scan, skip items that turned out to be empty (size 0). Unknown sizes (-1) still run.
        var selected = AllItems.Where(i => i.IsSelected && i.IsAvailable && (!HasScanned || i.SizeBytes != 0)).ToList();
        if (selected.Count == 0)
        {
            MainViewModel.Instance.ShowToast("Nothing to clean in the selected items.", ToastKind.Success);
            return;
        }

        static string Line(CleanerItemViewModel s) => s.SizeBytes > 0 ? $"{s.Name}  ·  {Format.Bytes(s.SizeBytes.Value)}" : s.Name;
        var caution = selected.Where(i => i.IsCaution).ToList();
        var sizeHint = SelectedBytes > 0 ? $" (about {SelectedBytesText})" : "";
        var body = caution.Count > 0
            ? DialogService.BulletList($"NewKlear will clean {selected.Count} items{sizeHint}. These advanced items cannot be undone:",
                caution.Select(c => $"{c.Name} — {c.Warning}"), "Make sure you are OK with these before continuing.")
            : DialogService.BulletList($"NewKlear will permanently delete these files{sizeHint}:", selected.Select(Line),
                HasScanned ? "Files that are in use are skipped automatically." : "Tip: press Scan first to see sizes. Files in use are skipped automatically.");

        if (!await DialogService.ConfirmAsync("Clean selected items?", body, "Clean now", danger: caution.Count > 0)) return;

        IsBusy = true;
        _cts = new CancellationTokenSource();
        long freed = 0;
        int deleted = 0, skipped = 0, index = 0;
        Progress = 0;
        var progress = new Progress<string>(p => StatusText = $"Cleaning… {p}");

        try
        {
            foreach (var item in selected)
            {
                _cts.Token.ThrowIfCancellationRequested();
                item.IsBusy = true;
                StatusText = $"Cleaning {item.Name}…";
                try
                {
                    var r = await item.Cleaner.CleanAsync(progress, _cts.Token);
                    freed += r.BytesFreed;
                    deleted += r.FilesDeleted;
                    skipped += r.FilesSkipped;
                    item.ResultText = r.Note ?? (r.BytesFreed > 0 ? $"Freed {Format.Bytes(r.BytesFreed)}" : "Done");
                    if (r.FilesSkipped > 0 && r.Note is null) item.ResultText += $" · {r.FilesSkipped} in use";
                    item.SizeBytes = Math.Max(0, (item.SizeBytes ?? 0) - r.BytesFreed);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Log.Error($"Clean failed for {item.Name}", ex);
                    item.ResultText = "Failed: " + ex.Message;
                }
                finally
                {
                    item.IsBusy = false;
                    Progress = 100.0 * ++index / selected.Count;
                }
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cleaning stopped.";
        }
        finally
        {
            IsBusy = false;
            LastResult = $"Freed {Format.Bytes(freed)}";
            StatusText = $"Done. Freed {Format.Bytes(freed)} · {deleted:N0} files removed" + (skipped > 0 ? $" · {skipped:N0} in use were skipped" : "");
            App.Settings.LastCleanTime = DateTime.Now;
            App.Settings.LastCleanBytes = freed;
            App.Settings.TotalBytesCleaned += freed;
            App.Settings.Save();
            HistoryStore.Add(HistoryKind.Cleaned, $"Cleaned {Format.Bytes(freed)}",
                $"{string.Join(", ", selected.Select(s => s.Name))}. {deleted:N0} files removed, {skipped:N0} skipped.");
            MainViewModel.Instance.ShowToast($"Cleaning complete — freed {Format.Bytes(freed)}", ToastKind.Success);
            RaiseTotals();
        }
    }

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand]
    private void SelectRecommended()
    {
        foreach (var i in AllItems) i.IsSelected = i.IsAvailable && i.Cleaner.DefaultSelected;
        RaiseTotals();
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var i in AllItems) i.IsSelected = false;
        RaiseTotals();
    }

    public override Task OnNavigatedToAsync()
    {
        RaiseTotals();
        return Task.CompletedTask;
    }
}
