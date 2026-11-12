using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewKlear.Core.Bloat;
using NewKlear.Core.History;
using NewKlear.Core.Infrastructure;
using NewKlear.Core.Tweaks;
using Wpf.Ui.Controls;

namespace NewKlear.App.ViewModels;

public partial class HistoryItemViewModel : ObservableObject
{
    public HistoryEntry Entry { get; }
    public string Title => Entry.Title;
    public string Detail => Entry.Detail;
    public string TimeText => Entry.Time.ToString("d MMM yyyy, HH:mm");
    public bool CanUndo => Entry.CanUndo;
    public bool IsUndone => Entry.Undone;

    public SymbolRegular Icon => Entry.Kind switch
    {
        HistoryKind.Cleaned => SymbolRegular.Broom24,
        HistoryKind.TweakApplied => SymbolRegular.TopSpeed24,
        HistoryKind.TweakReverted => SymbolRegular.ArrowUndo24,
        HistoryKind.AppRemoved => SymbolRegular.Delete24,
        HistoryKind.AppRestored => SymbolRegular.ArrowReset24,
        HistoryKind.StartupChanged => SymbolRegular.Rocket24,
        HistoryKind.PowerPlanChanged => SymbolRegular.Power24,
        HistoryKind.RestorePoint => SymbolRegular.ShieldCheckmark24,
        _ => SymbolRegular.Info24,
    };

    [ObservableProperty] private bool _isBusy;

    public HistoryItemViewModel(HistoryEntry entry) => Entry = entry;
}

public partial class HistoryViewModel : PageViewModel
{
    public ObservableCollection<HistoryItemViewModel> Items { get; } = [];

    public string LogFolder => AppPaths.Logs;
    public string BackupFolder => AppPaths.Backups;

    public HistoryViewModel()
    {
        HistoryStore.Changed += () => Application.Current?.Dispatcher.InvokeAsync(Reload);
    }

    public override Task OnNavigatedToAsync()
    {
        Reload();
        return Task.CompletedTask;
    }

    private void Reload()
    {
        Items.Clear();
        foreach (var e in HistoryStore.Load()) Items.Add(new HistoryItemViewModel(e));
        OnPropertyChanged(nameof(IsEmpty));
    }

    public bool IsEmpty => Items.Count == 0;

    [RelayCommand]
    private async Task UndoAsync(HistoryItemViewModel item)
    {
        var e = item.Entry;
        var main = MainViewModel.Instance;
        item.IsBusy = true;
        try
        {
            switch (e.Kind)
            {
                case HistoryKind.TweakApplied:
                {
                    var tweak = TweakCatalog.Build().FirstOrDefault(t => t.Id == e.Reference);
                    if (tweak is null) { main.ShowToast("This setting is no longer available on this PC.", ToastKind.Error); return; }
                    await tweak.RevertAsync();
                    HistoryStore.MarkTweakEntriesUndone(tweak.Id);
                    HistoryStore.Add(HistoryKind.TweakReverted, $"Reverted: {tweak.Name}", "", tweak.Id);
                    main.Optimize.NoteEffect(tweak.Effect);
                    await main.Optimize.RefreshStateAsync();
                    main.ShowToast($"Reverted: {tweak.Name}", ToastKind.Success);
                    break;
                }
                case HistoryKind.AppRemoved:
                {
                    var name = e.Reference ?? "";
                    if (await AppxService.TryReinstallAsync(name))
                    {
                        HistoryStore.MarkUndone(e.Id);
                        HistoryStore.Add(HistoryKind.AppRestored, $"Restored {e.Reference2 ?? name}", name);
                        main.ShowToast($"Restored {e.Reference2 ?? name}", ToastKind.Success);
                    }
                    else
                    {
                        main.ShowToast("Windows no longer has a copy of this app. Opening the Microsoft Store so you can reinstall it.", ToastKind.Info);
                        Process.Start(new ProcessStartInfo(AppxService.StoreSearchUri(e.Reference2 ?? name)) { UseShellExecute = true });
                    }
                    break;
                }
                case HistoryKind.PowerPlanChanged when Guid.TryParse(e.Reference, out var plan):
                {
                    if (await PowerPlanService.ActivateAsync(plan))
                    {
                        HistoryStore.MarkUndone(e.Id);
                        HistoryStore.Add(HistoryKind.PowerPlanChanged, $"Power plan restored: {e.Reference2}", "");
                        main.ShowToast($"Power plan restored to {e.Reference2}", ToastKind.Success);
                    }
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("Undo failed", ex);
            main.ShowToast("Undo failed: " + ex.Message, ToastKind.Error);
        }
        finally
        {
            item.IsBusy = false;
            Reload();
        }
    }

    [RelayCommand]
    private void OpenLogs() => OpenFolder(AppPaths.Logs);

    [RelayCommand]
    private void OpenBackups() => OpenFolder(AppPaths.Backups);

    [RelayCommand]
    private async Task CreateRestorePointAsync() => await TweakActions.CreateRestorePointAsync();

    [RelayCommand]
    private void OpenSystemRestore()
    {
        try { Process.Start(new ProcessStartInfo(ProcessRunner.System32("rstrui.exe")) { UseShellExecute = true }); }
        catch (Exception ex) { MainViewModel.Instance.ShowToast(ex.Message, ToastKind.Error); }
    }

    public static void OpenFolder(string path)
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true }); }
        catch (Exception ex) { Log.Warn($"Open folder failed: {ex.Message}"); }
    }
}
