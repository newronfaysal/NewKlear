using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewKlear.App.Services;
using NewKlear.Core.Cleaning;
using NewKlear.Core.History;
using NewKlear.Core.Infrastructure;
using NewKlear.Core.Tweaks;

namespace NewKlear.App.ViewModels;

/// <summary>Applies/reverts tweaks with history logging and the "restore point first" safety prompt.</summary>
public static class TweakActions
{
    private static bool _restorePointHandledThisSession;

    /// <summary>Returns false if the user cancelled.</summary>
    public static async Task<bool> EnsureRestorePointAsync()
    {
        if (_restorePointHandledThisSession || !App.Settings.PromptRestorePointBeforeTweaks || !Elevation.IsAdministrator) return true;

        var choice = await DialogService.ShowAsync("Create a restore point first?",
            "A System Restore point lets you roll Windows back if anything goes wrong. NewKlear also backs up every setting it changes, so each tweak can be undone individually.",
            "Create & continue", "Continue without", "Cancel");
        if (choice == DialogChoice.Cancel) return false;
        _restorePointHandledThisSession = true;
        if (choice == DialogChoice.Primary) await CreateRestorePointAsync();
        return true;
    }

    public static async Task<bool> CreateRestorePointAsync()
    {
        var main = MainViewModel.Instance;
        main.ShowToast("Creating restore point… this can take a minute.");
        var r = await RestorePointService.CreateAsync($"NewKlear {DateTime.Now:yyyy-MM-dd HH:mm}");
        if (r.Status == RestorePointStatus.ProtectionDisabled)
        {
            if (await DialogService.ConfirmAsync("System Protection is off",
                    "System Restore is turned off for your system drive, so no restore point can be made. Turn it on now? Windows will reserve a small amount of disk space for restore points.",
                    "Turn on & create"))
            {
                if (await RestorePointService.EnableProtectionAsync())
                    r = await RestorePointService.CreateAsync($"NewKlear {DateTime.Now:yyyy-MM-dd HH:mm}");
            }
        }

        if (r.Status == RestorePointStatus.Created)
        {
            _restorePointHandledThisSession = true;
            HistoryStore.Add(HistoryKind.RestorePoint, "Created System Restore point");
            main.ShowToast("Restore point created", ToastKind.Success);
            return true;
        }
        main.ShowToast("Restore point not created: " + r.Message, ToastKind.Error);
        return false;
    }

    public static async Task ApplyAsync(ITweak tweak)
    {
        await tweak.ApplyAsync();
        HistoryStore.Add(HistoryKind.TweakApplied, tweak.Name, tweak.Description, tweak.Id);
    }

    public static async Task RevertAsync(ITweak tweak)
    {
        await tweak.RevertAsync();
        HistoryStore.MarkTweakEntriesUndone(tweak.Id);
        HistoryStore.Add(HistoryKind.TweakReverted, $"Reverted: {tweak.Name}", "", tweak.Id);
    }
}

public partial class TweakItemViewModel : ObservableObject
{
    private readonly OptimizeViewModel _owner;
    private bool _suppress;

    public ITweak Tweak { get; }
    public string Name => Tweak.Name;
    public string Description => Tweak.Description;
    public bool IsCaution => Tweak.Risk == Risk.Caution;
    public bool CanChange { get; }
    public string Category => Tweak.Category.ToString();

    public string? EffectText => Tweak.Effect switch
    {
        ApplyEffect.RestartExplorer => "Restart Explorer",
        ApplyEffect.SignOut => "Sign out to apply",
        ApplyEffect.Restart => "Restart to apply",
        _ => null,
    };

    [ObservableProperty] private bool _isOn;
    [ObservableProperty] private bool _isBusy;

    public TweakItemViewModel(ITweak tweak, OptimizeViewModel owner, bool isAdmin)
    {
        Tweak = tweak;
        _owner = owner;
        CanChange = !tweak.RequiresAdmin || isAdmin;
    }

    public void SetState(bool on)
    {
        _suppress = true;
        IsOn = on;
        _suppress = false;
    }

    partial void OnIsOnChanged(bool value)
    {
        if (_suppress) return;
        _ = ToggleAsync(value);
    }

    private async Task ToggleAsync(bool value)
    {
        if (value && !await TweakActions.EnsureRestorePointAsync())
        {
            SetState(false);
            return;
        }

        IsBusy = true;
        try
        {
            if (value) await TweakActions.ApplyAsync(Tweak);
            else await TweakActions.RevertAsync(Tweak);
            _owner.NoteEffect(Tweak.Effect);
            MainViewModel.Instance.ShowToast($"{(value ? "Applied" : "Reverted")}: {Name}", ToastKind.Success);
        }
        catch (Exception ex)
        {
            Log.Error($"Tweak '{Name}' failed", ex);
            MainViewModel.Instance.ShowToast($"Could not change '{Name}': {ex.Message}", ToastKind.Error);
            SetState(Tweak.IsApplied());
        }
        finally
        {
            IsBusy = false;
            _owner.RaiseCounts();
        }
    }
}

public partial class PowerPlanOption : ObservableObject
{
    public required Guid Template { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    [ObservableProperty] private bool _isActive;
}

public partial class OptimizeViewModel : PageViewModel
{
    private readonly ObservableCollection<TweakItemViewModel> _tweaks = [];
    private bool _loaded;

    public ICollectionView Tweaks { get; }
    public ObservableCollection<PowerPlanOption> PowerPlans { get; } =
    [
        new() { Template = PowerPlanService.Balanced, Name = "Balanced", Description = "Windows default. Saves power when idle." },
        new() { Template = PowerPlanService.HighPerformance, Name = "High performance", Description = "Keeps the CPU ready. Uses more power." },
        new() { Template = PowerPlanService.UltimatePerformance, Name = "Ultimate performance", Description = "Removes power-saving latency. Best for desktops." },
    ];

    public bool IsAdmin { get; } = Elevation.IsAdministrator;

    [ObservableProperty] private string _category = "Performance";
    [ObservableProperty] private string _activePlanName = "";
    [ObservableProperty] private bool _needsExplorerRestart;
    [ObservableProperty] private bool _needsSignOut;
    [ObservableProperty] private bool _needsRestart;
    [ObservableProperty] private bool _isBusy;

    public int AppliedCount => _tweaks.Count(t => t.IsOn);
    public int TotalCount => _tweaks.Count;

    public string PendingText => NeedsRestart ? "Restart your PC to finish applying some changes."
        : NeedsSignOut ? "Sign out and back in to finish applying some changes."
        : NeedsExplorerRestart ? "Restart Explorer to see some changes." : "";

    public OptimizeViewModel()
    {
        Tweaks = CollectionViewSource.GetDefaultView(_tweaks);
        Tweaks.Filter = o => o is TweakItemViewModel t && t.Category == Category;
    }

    partial void OnCategoryChanged(string value) => Tweaks.Refresh();
    partial void OnNeedsExplorerRestartChanged(bool value) => OnPropertyChanged(nameof(PendingText));
    partial void OnNeedsSignOutChanged(bool value) => OnPropertyChanged(nameof(PendingText));
    partial void OnNeedsRestartChanged(bool value) => OnPropertyChanged(nameof(PendingText));

    public void NoteEffect(ApplyEffect effect)
    {
        switch (effect)
        {
            case ApplyEffect.RestartExplorer: NeedsExplorerRestart = true; break;
            case ApplyEffect.SignOut: NeedsSignOut = true; break;
            case ApplyEffect.Restart: NeedsRestart = true; break;
        }
    }

    public void RaiseCounts()
    {
        OnPropertyChanged(nameof(AppliedCount));
        OnPropertyChanged(nameof(TotalCount));
    }

    public override async Task OnNavigatedToAsync()
    {
        if (!_loaded)
        {
            var admin = IsAdmin;
            var tweaks = await Task.Run(TweakCatalog.Build);
            foreach (var t in tweaks) _tweaks.Add(new TweakItemViewModel(t, this, admin));
            _loaded = true;
        }
        await RefreshStateAsync();
    }

    public async Task RefreshStateAsync()
    {
        var states = await Task.Run(() => _tweaks.Select(t => t.Tweak.IsApplied()).ToList());
        for (var i = 0; i < _tweaks.Count; i++) _tweaks[i].SetState(states[i]);
        RaiseCounts();
        await RefreshPowerPlanAsync();
    }

    private async Task RefreshPowerPlanAsync()
    {
        try
        {
            var active = await PowerPlanService.GetActiveAsync();
            ActivePlanName = active?.Name ?? "Unknown";
            foreach (var p in PowerPlans)
            {
                p.IsActive = active != null && (active.Id == p.Template
                    || (p.Template == PowerPlanService.UltimatePerformance && active.Name.Contains("Ultimate", StringComparison.OrdinalIgnoreCase))
                    || (p.Template == PowerPlanService.HighPerformance && active.Name.Equals("High performance", StringComparison.OrdinalIgnoreCase)));
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not read power plan: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SetPowerPlanAsync(PowerPlanOption option)
    {
        if (!IsAdmin)
        {
            MainViewModel.Instance.ShowToast("Changing the power plan needs administrator rights.", ToastKind.Error);
            return;
        }
        var previous = await PowerPlanService.GetActiveAsync();
        IsBusy = true;
        var ok = await PowerPlanService.ActivateBuiltInAsync(option.Template);
        IsBusy = false;
        await RefreshPowerPlanAsync();
        if (ok)
        {
            HistoryStore.Add(HistoryKind.PowerPlanChanged, $"Power plan: {option.Name}", $"Previously {previous?.Name}", previous?.Id.ToString(), previous?.Name);
            MainViewModel.Instance.ShowToast($"Power plan set to {option.Name}", ToastKind.Success);
        }
        else
        {
            MainViewModel.Instance.ShowToast($"{option.Name} isn't available on this PC (common on laptops with Modern Standby).", ToastKind.Error);
        }
    }

    private static readonly string[] RecommendedIds =
    [
        "perf.startupdelay", "perf.gamedvr", "perf.edgebg", "perf.storagesense", "perf.dopeer",
        "priv.telemetry", "priv.tailored", "priv.adid", "priv.activity", "priv.feedback", "priv.suggestions", "priv.bing",
        "exp.extensions", "exp.endtask", "svc.diagtrack", "svc.retaildemo",
    ];

    [RelayCommand]
    private async Task ApplyRecommendedAsync()
    {
        var targets = _tweaks.Where(t => RecommendedIds.Contains(t.Tweak.Id) && !t.IsOn && t.CanChange).ToList();
        if (targets.Count == 0)
        {
            MainViewModel.Instance.ShowToast("All recommended optimizations are already applied.", ToastKind.Success);
            return;
        }
        var body = DialogService.BulletList("These safe, commonly recommended settings will be applied. Each one can be reverted later:", targets.Select(t => t.Name));
        if (!await DialogService.ConfirmAsync("Apply recommended optimizations?", body, "Apply all")) return;
        if (!await TweakActions.EnsureRestorePointAsync()) return;

        IsBusy = true;
        var failed = 0;
        foreach (var t in targets)
        {
            t.IsBusy = true;
            try
            {
                await TweakActions.ApplyAsync(t.Tweak);
                t.SetState(true);
                NoteEffect(t.Tweak.Effect);
            }
            catch (Exception ex)
            {
                failed++;
                Log.Error($"Tweak '{t.Name}' failed", ex);
            }
            t.IsBusy = false;
        }
        IsBusy = false;
        RaiseCounts();
        MainViewModel.Instance.ShowToast(failed == 0 ? $"Applied {targets.Count} optimizations" : $"Applied {targets.Count - failed}, {failed} failed (see log)",
            failed == 0 ? ToastKind.Success : ToastKind.Error);
    }

    [RelayCommand]
    private async Task CreateRestorePointAsync()
    {
        IsBusy = true;
        await TweakActions.CreateRestorePointAsync();
        IsBusy = false;
    }

    [RelayCommand]
    private async Task RestartExplorerAsync()
    {
        IsBusy = true;
        await ShellHelper.RestartExplorerAsync();
        NeedsExplorerRestart = false;
        IsBusy = false;
    }
}
