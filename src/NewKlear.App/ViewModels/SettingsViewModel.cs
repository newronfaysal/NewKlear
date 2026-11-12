using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewKlear.App.Services;
using NewKlear.Core.Infrastructure;

namespace NewKlear.App.ViewModels;

public partial class ThemeOptionViewModel : ObservableObject
{
    public ThemeDefinition Theme { get; }
    [ObservableProperty] private bool _isSelected;
    public ThemeOptionViewModel(ThemeDefinition theme) => Theme = theme;
}

public partial class SettingsViewModel : PageViewModel
{
    public ObservableCollection<ThemeOptionViewModel> Themes { get; }

    public string Version { get; } = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
    public string DataFolder => AppPaths.Root;
    public bool IsAdmin { get; } = Elevation.IsAdministrator;

    [ObservableProperty] private bool _includeDeveloperCaches = App.Settings.IncludeDeveloperCaches;
    [ObservableProperty] private bool _deprovisionAppsByDefault = App.Settings.DeprovisionAppsByDefault;
    [ObservableProperty] private bool _promptRestorePoint = App.Settings.PromptRestorePointBeforeTweaks;

    public SettingsViewModel()
    {
        Themes = new ObservableCollection<ThemeOptionViewModel>(ThemeService.Themes.Select(t => new ThemeOptionViewModel(t)
        {
            IsSelected = t.Id == ThemeService.Current.Id,
        }));
    }

    [RelayCommand]
    private void SelectTheme(ThemeOptionViewModel option)
    {
        foreach (var t in Themes) t.IsSelected = t == option;
        ThemeService.Apply(option.Theme.Id);
        App.Settings.ThemeId = option.Theme.Id;
        App.Settings.Save();
    }

    partial void OnIncludeDeveloperCachesChanged(bool value)
    {
        App.Settings.IncludeDeveloperCaches = value;
        App.Settings.Save();
        MainViewModel.Instance.Clean.Rebuild();
    }

    partial void OnDeprovisionAppsByDefaultChanged(bool value)
    {
        App.Settings.DeprovisionAppsByDefault = value;
        App.Settings.Save();
        MainViewModel.Instance.Bloatware.AlsoDeprovision = value && IsAdmin;
    }

    partial void OnPromptRestorePointChanged(bool value)
    {
        App.Settings.PromptRestorePointBeforeTweaks = value;
        App.Settings.Save();
    }

    [RelayCommand]
    private void OpenDataFolder() => HistoryViewModel.OpenFolder(AppPaths.Root);
}
