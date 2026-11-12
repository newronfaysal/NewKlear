using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewKlear.Core.History;
using NewKlear.Core.Infrastructure;
using NewKlear.Core.Startup;

namespace NewKlear.App.ViewModels;

public partial class StartupItemViewModel : ObservableObject
{
    private readonly StartupViewModel _owner;
    private bool _suppress;

    public StartupEntry Entry { get; }
    public string Name => Entry.Name;
    public string Publisher => string.IsNullOrEmpty(Entry.Publisher) ? "Unknown publisher" : Entry.Publisher;
    public string Command => Entry.Command;
    public string SourceLabel => Entry.SourceLabel;
    public bool CanChange { get; }
    public string Initial => string.IsNullOrEmpty(Name) ? "?" : Name[..1].ToUpperInvariant();

    [ObservableProperty] private bool _isEnabled;

    public StartupItemViewModel(StartupEntry entry, StartupViewModel owner, bool isAdmin)
    {
        Entry = entry;
        _owner = owner;
        CanChange = !entry.RequiresAdmin || isAdmin;
        _suppress = true;
        IsEnabled = entry.Enabled;
        _suppress = false;
    }

    partial void OnIsEnabledChanged(bool value)
    {
        if (_suppress) return;
        try
        {
            StartupManager.SetEnabled(Entry, value);
            HistoryStore.Add(HistoryKind.StartupChanged, $"{(value ? "Enabled" : "Disabled")} startup app {Name}", Entry.SourceLabel);
            MainViewModel.Instance.ShowToast($"{Name} will {(value ? "" : "no longer ")}start with Windows", ToastKind.Success);
        }
        catch (Exception ex)
        {
            Log.Error($"Startup toggle failed for {Name}", ex);
            MainViewModel.Instance.ShowToast($"Could not change {Name}: {ex.Message}", ToastKind.Error);
            _suppress = true;
            IsEnabled = !value;
            _suppress = false;
        }
        _owner.RaiseCounts();
    }

    [RelayCommand]
    private void OpenLocation()
    {
        var path = Entry.FilePath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            MainViewModel.Instance.ShowToast("The file for this entry could not be found.", ToastKind.Error);
            return;
        }
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }
}

public partial class StartupViewModel : PageViewModel
{
    public ObservableCollection<StartupItemViewModel> Items { get; } = [];

    [ObservableProperty] private bool _isBusy;

    public int EnabledCount => Items.Count(i => i.IsEnabled);
    public int DisabledCount => Items.Count(i => !i.IsEnabled);

    public void RaiseCounts()
    {
        OnPropertyChanged(nameof(EnabledCount));
        OnPropertyChanged(nameof(DisabledCount));
    }

    public override Task OnNavigatedToAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        var admin = Elevation.IsAdministrator;
        var entries = await Task.Run(StartupManager.GetEntries);
        Items.Clear();
        foreach (var e in entries) Items.Add(new StartupItemViewModel(e, this, admin));
        IsBusy = false;
        RaiseCounts();
    }

    [RelayCommand]
    private void OpenTaskManager()
    {
        try { Process.Start(new ProcessStartInfo("taskmgr.exe", "/0 /startup") { UseShellExecute = true }); }
        catch (Exception ex) { Log.Warn($"Could not open Task Manager: {ex.Message}"); }
    }
}
