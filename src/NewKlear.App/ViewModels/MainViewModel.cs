using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewKlear.Core.Infrastructure;
using Wpf.Ui.Controls;

namespace NewKlear.App.ViewModels;

public abstract class PageViewModel : ObservableObject
{
    public virtual Task OnNavigatedToAsync() => Task.CompletedTask;
}

public sealed record NavItem(string Title, SymbolRegular Icon, PageViewModel Page);

public enum ToastKind
{
    Info,
    Success,
    Error,
}

public partial class MainViewModel : ObservableObject
{
    public static MainViewModel Instance { get; private set; } = null!;

    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(4.5) };

    public DashboardViewModel Dashboard { get; }
    public CleanViewModel Clean { get; }
    public BloatwareViewModel Bloatware { get; }
    public StartupViewModel Startup { get; }
    public OptimizeViewModel Optimize { get; }
    public HistoryViewModel History { get; }
    public SettingsViewModel Settings { get; }

    public ObservableCollection<NavItem> NavItems { get; }
    public ObservableCollection<NavItem> FooterItems { get; }

    public bool IsAdmin { get; } = Elevation.IsAdministrator;

    [ObservableProperty] private NavItem? _selectedNav;
    [ObservableProperty] private NavItem? _selectedFooter;
    [ObservableProperty] private PageViewModel? _currentPage;
    [ObservableProperty] private string? _toastMessage;
    [ObservableProperty] private ToastKind _toastKind;
    [ObservableProperty] private bool _isToastVisible;

    public MainViewModel()
    {
        Instance = this;
        Clean = new CleanViewModel();
        Dashboard = new DashboardViewModel(Clean);
        Bloatware = new BloatwareViewModel();
        Startup = new StartupViewModel();
        Optimize = new OptimizeViewModel();
        History = new HistoryViewModel();
        Settings = new SettingsViewModel();

        NavItems =
        [
            new("Dashboard", SymbolRegular.Home24, Dashboard),
            new("Clean", SymbolRegular.Broom24, Clean),
            new("Bloatware", SymbolRegular.AppsList24, Bloatware),
            new("Startup", SymbolRegular.Rocket24, Startup),
            new("Optimize", SymbolRegular.TopSpeed24, Optimize),
        ];
        FooterItems =
        [
            new("History & Undo", SymbolRegular.History24, History),
            new("Settings", SymbolRegular.Settings24, Settings),
        ];

        _toastTimer.Tick += (_, _) => { IsToastVisible = false; _toastTimer.Stop(); };
        SelectedNav = NavItems[0];
    }

    partial void OnSelectedNavChanged(NavItem? value)
    {
        if (value is null) return;
        SelectedFooter = null;
        Show(value.Page);
    }

    partial void OnSelectedFooterChanged(NavItem? value)
    {
        if (value is null) return;
        SelectedNav = null;
        Show(value.Page);
    }

    private async void Show(PageViewModel page)
    {
        CurrentPage = page;
        try { await page.OnNavigatedToAsync(); }
        catch (Exception ex) { Log.Error("Page load failed", ex); ShowToast(ex.Message, ToastKind.Error); }
    }

    public void NavigateTo(PageViewModel page)
    {
        var nav = NavItems.FirstOrDefault(n => n.Page == page);
        if (nav != null) { SelectedNav = nav; return; }
        var footer = FooterItems.FirstOrDefault(n => n.Page == page);
        if (footer != null) SelectedFooter = footer;
    }

    public void ShowToast(string message, ToastKind kind = ToastKind.Info)
    {
        ToastMessage = message;
        ToastKind = kind;
        IsToastVisible = true;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    [RelayCommand]
    private void DismissToast() => IsToastVisible = false;

    [RelayCommand]
    private void RestartAsAdmin()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" });
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Log.Warn($"Elevation cancelled: {ex.Message}");
        }
    }

    [RelayCommand]
    private void NavigateToPage(PageViewModel page) => NavigateTo(page);
}
