using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewKlear.App.Services;
using NewKlear.Core.Bloat;
using NewKlear.Core.History;
using NewKlear.Core.Infrastructure;

namespace NewKlear.App.ViewModels;

public partial class AppItemViewModel : ObservableObject
{
    public InstalledApp App { get; }
    public string Name => App.FriendlyName;
    public string PackageName => App.PackageName;
    public string Description => App.Description;
    public BloatTag Tag => App.Tag;
    public bool IsCopilot => App.IsCopilot;
    public bool IsProtected => App.Tag == BloatTag.Protected;
    public string TagText => App.Tag switch
    {
        BloatTag.Recommended => "Recommended",
        BloatTag.Optional => "Optional",
        BloatTag.Protected => "Keep",
        _ => "Unlisted",
    };

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _status;
    [ObservableProperty] private bool _isRemoved;

    public AppItemViewModel(InstalledApp app) => App = app;
}

public partial class BloatwareViewModel : PageViewModel
{
    private bool _loaded;
    private readonly ObservableCollection<AppItemViewModel> _apps = [];

    public ICollectionView Apps { get; }

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _filter = "all";
    [ObservableProperty] private bool _showProtected;
    [ObservableProperty] private bool _alsoDeprovision;
    [ObservableProperty] private bool _hasCopilot;

    public bool IsAdmin { get; } = Elevation.IsAdministrator;

    public int SelectedCount => _apps.Count(a => a.IsSelected && !a.IsRemoved);
    public int RecommendedCount => _apps.Count(a => a.Tag == BloatTag.Recommended && !a.IsRemoved);

    public BloatwareViewModel()
    {
        Apps = CollectionViewSource.GetDefaultView(_apps);
        Apps.Filter = o => o is AppItemViewModel a && Matches(a);
        _alsoDeprovision = App.Settings.DeprovisionAppsByDefault && IsAdmin;
    }

    private bool Matches(AppItemViewModel a)
    {
        if (a.IsRemoved) return false;
        if (a.IsProtected && !ShowProtected) return false;
        var tagOk = Filter switch
        {
            "recommended" => a.Tag == BloatTag.Recommended,
            "optional" => a.Tag == BloatTag.Optional,
            "unlisted" => a.Tag == BloatTag.Unlisted,
            "copilot" => a.IsCopilot,
            _ => true,
        };
        if (!tagOk) return false;
        return string.IsNullOrWhiteSpace(SearchText)
               || a.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
               || a.PackageName.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    partial void OnSearchTextChanged(string value) => Apps.Refresh();
    partial void OnFilterChanged(string value) => Apps.Refresh();
    partial void OnShowProtectedChanged(bool value) => Apps.Refresh();

    public override async Task OnNavigatedToAsync()
    {
        if (!_loaded) await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusText = "Reading installed apps…";
        try
        {
            var apps = await AppxService.ListAsync();
            foreach (var old in _apps) old.PropertyChanged -= ItemChanged;
            _apps.Clear();
            foreach (var app in apps)
            {
                var vm = new AppItemViewModel(app);
                vm.PropertyChanged += ItemChanged;
                _apps.Add(vm);
            }
            _loaded = true;
            HasCopilot = _apps.Any(a => a.IsCopilot);
            StatusText = $"{_apps.Count(a => !a.IsProtected)} removable apps found · {RecommendedCount} recommended to remove";
        }
        catch (Exception ex)
        {
            StatusText = "Could not read installed apps.";
            MainViewModel.Instance.ShowToast(ex.Message, ToastKind.Error);
        }
        finally
        {
            IsBusy = false;
            Apps.Refresh();
            RaiseCounts();
        }
    }

    private void ItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppItemViewModel.IsSelected)) RaiseCounts();
    }

    private void RaiseCounts()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(RecommendedCount));
        RemoveSelectedCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void SelectRecommended()
    {
        foreach (var a in _apps) a.IsSelected = a.Tag == BloatTag.Recommended && !a.IsRemoved;
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var a in _apps) a.IsSelected = false;
    }

    private bool CanRemove() => !IsBusy && SelectedCount > 0;

    partial void OnIsBusyChanged(bool value) => RemoveSelectedCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private Task RemoveSelectedAsync() => RemoveAsync(_apps.Where(a => a.IsSelected && !a.IsRemoved).ToList());

    [RelayCommand]
    private Task RemoveCopilotAsync() => RemoveAsync(_apps.Where(a => a.IsCopilot && !a.IsRemoved).ToList());

    private async Task RemoveAsync(List<AppItemViewModel> targets)
    {
        if (targets.Count == 0) return;

        var unlisted = targets.Count(t => t.Tag is BloatTag.Unlisted or BloatTag.Protected);
        var footer = (AlsoDeprovision ? "Apps will also be removed for new user accounts. " : "")
                     + (unlisted > 0 ? $"{unlisted} selected app(s) are not on NewKlear's recommended list. " : "")
                     + "Most apps can be restored from History & Undo or reinstalled from the Microsoft Store.";
        var body = DialogService.BulletList($"Remove {targets.Count} app(s) from this PC?", targets.Select(t => t.Name), footer);
        if (!await DialogService.ConfirmAsync("Remove apps", body, "Remove", danger: true)) return;

        IsBusy = true;
        var ok = 0;
        var index = 0;
        foreach (var t in targets)
        {
            index++;
            StatusText = $"Removing {t.Name} ({index} of {targets.Count})…";
            t.IsBusy = true;
            var r = await AppxService.RemoveAsync(t.App, AlsoDeprovision && IsAdmin);
            t.IsBusy = false;
            if (r.Success)
            {
                ok++;
                t.IsSelected = false;
                t.IsRemoved = true;
                HistoryStore.Add(HistoryKind.AppRemoved, $"Removed {t.Name}", t.PackageName, t.PackageName, t.Name);
            }
            else
            {
                t.Status = "Failed: " + r.Error;
            }
        }

        IsBusy = false;
        HasCopilot = _apps.Any(a => a.IsCopilot && !a.IsRemoved);
        Apps.Refresh();
        RaiseCounts();
        StatusText = $"Removed {ok} of {targets.Count} app(s).";
        MainViewModel.Instance.ShowToast(StatusText, ok == targets.Count ? ToastKind.Success : ToastKind.Error);
    }

    [RelayCommand]
    private void OpenCopilotTweak()
    {
        var main = MainViewModel.Instance;
        main.Optimize.Category = "Privacy";
        main.NavigateTo(main.Optimize);
    }
}
