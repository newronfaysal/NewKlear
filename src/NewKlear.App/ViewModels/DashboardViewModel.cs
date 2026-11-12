using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewKlear.Core.Infrastructure;
using NewKlear.Core.Startup;
using NewKlear.Core.SystemInfo;
using NewKlear.Core.Tweaks;

namespace NewKlear.App.ViewModels;

public sealed record DriveItem(string Name, string Label, string UsedText, double Percent);

public partial class DashboardViewModel : PageViewModel
{
    private readonly CpuSampler _cpu = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };

    public CleanViewModel Clean { get; }

    public string OsName { get; } = SystemInfoService.OsName;
    public string CpuName { get; } = SystemInfoService.CpuName;
    public string Greeting => DateTime.Now.Hour switch
    {
        < 12 => "Good morning",
        < 18 => "Good afternoon",
        _ => "Good evening",
    };

    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private double _memoryPercent;
    [ObservableProperty] private string _memoryText = "";
    [ObservableProperty] private string _uptimeText = "";
    [ObservableProperty] private double _systemDrivePercent;
    [ObservableProperty] private string _systemDriveText = "";
    [ObservableProperty] private string _systemDriveFree = "";
    [ObservableProperty] private int _startupEnabledCount;
    [ObservableProperty] private string _tweaksAppliedText = "";
    [ObservableProperty] private string _totalCleanedText = "";
    [ObservableProperty] private string _lastCleanText = "";

    public ObservableCollection<DriveItem> Drives { get; } = [];

    public DashboardViewModel(CleanViewModel clean)
    {
        Clean = clean;
        _timer.Tick += (_, _) => RefreshLive();
        RefreshLive();
        RefreshDrives();
    }

    public string HeroTitle => Clean.IsScanning ? "Scanning your PC…"
        : Clean.HasScanned ? (Clean.TotalFoundBytes > 0 ? $"{Format.Bytes(Clean.TotalFoundBytes)} of junk found" : "Your PC is spotless")
        : "Ready to clean";

    public string HeroSubtitle => Clean.IsScanning ? "Looking through temp files, caches and leftovers."
        : Clean.HasScanned ? $"{Format.Bytes(Clean.SelectedBytes)} is selected and safe to remove. Review it on the Clean page."
        : "Run a quick scan to see how much space temporary files, caches and leftovers are taking.";

    private void RefreshLive()
    {
        CpuPercent = Math.Round(_cpu.Sample());
        var mem = SystemInfoService.GetMemory();
        MemoryPercent = Math.Round(mem.UsedPercent);
        MemoryText = $"{Format.Bytes((long)mem.Used)} of {Format.Bytes((long)mem.Total)}";
        UptimeText = Format.Duration(SystemInfoService.Uptime);
        OnPropertyChanged(nameof(HeroTitle));
        OnPropertyChanged(nameof(HeroSubtitle));
    }

    private void RefreshDrives()
    {
        Drives.Clear();
        var sys = Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\');
        foreach (var d in SystemInfoService.GetDrives())
        {
            Drives.Add(new DriveItem(d.Name, d.Label, $"{Format.Bytes(d.Free)} free of {Format.Bytes(d.Total)}", Math.Round(d.UsedPercent)));
            if (string.Equals(d.Name, sys, StringComparison.OrdinalIgnoreCase))
            {
                SystemDrivePercent = Math.Round(d.UsedPercent);
                SystemDriveText = $"{Format.Bytes(d.Used)} of {Format.Bytes(d.Total)} used";
                SystemDriveFree = $"{Format.Bytes(d.Free)} free";
            }
        }
    }

    public override async Task OnNavigatedToAsync()
    {
        _timer.Start();
        RefreshDrives();
        var s = App.Settings;
        TotalCleanedText = Format.Bytes(s.TotalBytesCleaned);
        LastCleanText = s.LastCleanTime is { } t ? $"Last clean {t:d MMM, HH:mm} · {Format.Bytes(s.LastCleanBytes)}" : "Not cleaned yet";

        await Task.Run(() =>
        {
            var startup = StartupManager.GetEntries().Count(e => e.Enabled);
            var tweaks = TweakCatalog.Build();
            var applied = tweaks.Count(t => t.IsApplied());
            return (startup, applied, tweaks.Count);
        }).ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully)
            {
                StartupEnabledCount = t.Result.startup;
                TweaksAppliedText = $"{t.Result.applied} of {t.Result.Item3} applied";
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    [RelayCommand]
    private async Task QuickScanAsync()
    {
        var watcher = new System.ComponentModel.PropertyChangedEventHandler((_, _) =>
        {
            OnPropertyChanged(nameof(HeroTitle));
            OnPropertyChanged(nameof(HeroSubtitle));
        });
        Clean.PropertyChanged += watcher;
        try
        {
            await Clean.ScanAsync();
        }
        finally
        {
            Clean.PropertyChanged -= watcher;
            OnPropertyChanged(nameof(HeroTitle));
            OnPropertyChanged(nameof(HeroSubtitle));
            RefreshDrives();
        }
    }

    [RelayCommand]
    private void Go(string page)
    {
        var main = MainViewModel.Instance;
        PageViewModel target = page switch
        {
            "clean" => main.Clean,
            "bloat" => main.Bloatware,
            "startup" => main.Startup,
            "optimize" => main.Optimize,
            _ => main.Dashboard,
        };
        main.NavigateTo(target);
    }
}
