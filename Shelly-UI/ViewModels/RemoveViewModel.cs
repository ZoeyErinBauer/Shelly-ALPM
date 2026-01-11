using System.Reactive.Concurrency;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using PackageManager.Alpm;
using ReactiveUI;
using Shelly_UI.Models;
using Shelly_UI.Services;

namespace Shelly_UI.ViewModels;

public class RemoveViewModel : ViewModelBase, IRoutableViewModel
{
    public IScreen HostScreen { get; }
    private IAlpmManager _alpmManager = AlpmService.Instance;
    private string? _searchText;
    private readonly ObservableAsPropertyHelper<IEnumerable<PackageModel>> _filteredPackages;

    public RemoveViewModel(IScreen screen)
    {
        HostScreen = screen;
        AvailablePackages = new ObservableCollection<PackageModel>();

        _filteredPackages = this
            .WhenAnyValue(x => x.SearchText, x => x.AvailablePackages.Count, (s, c) => s)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(Search)
            .ToProperty(this, x => x.FilteredPackages);

        RemovePackagesCommand = ReactiveCommand.CreateFromTask(RemovePackages);
        RefreshCommand = ReactiveCommand.CreateFromTask(Refresh);

        LoadData();
    }

    private async Task Refresh()
    {
        try
        {
            await Task.Run(() => _alpmManager.Initialize());
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                AvailablePackages.Clear();
                LoadData();
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to refresh installed packages: {e.Message}");
        }
    }

    private async void LoadData()
    {
        try
        {
            await Task.Run(() => _alpmManager.Initialize());
            var packages = await Task.Run(() => _alpmManager.GetInstalledPackages());
            var models = packages.Select(u => new PackageModel
            {
                Name = u.Name,
                Version = u.Version,
                DownloadSize = u.Size,
                IsChecked = false
            }).ToList();
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                foreach (var pkg in models)
                {
                    AvailablePackages.Add(pkg);
                }
                this.RaisePropertyChanged(nameof(AvailablePackages));
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load installed packages for removal: {e.Message}");
        }
    }

    private IEnumerable<PackageModel> Search(string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return AvailablePackages;
        }

        return AvailablePackages.Where(p => 
            p.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            p.Version.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }
    
    private bool _showConfirmDialog;
    public bool ShowConfirmDialog
    {
        get => _showConfirmDialog;
        set => this.RaiseAndSetIfChanged(ref _showConfirmDialog, value);
    }

    public void ToggleConfirmAction()
    {
        ShowConfirmDialog = !ShowConfirmDialog;
    }

    private async Task RemovePackages()
    {
        var selectedPackages = AvailablePackages.Where(x => x.IsChecked).Select(x => x.Name).ToList();
        if (selectedPackages.Any())
        {
            await Task.Run(() => _alpmManager.RemovePackages(selectedPackages));
        }
        ToggleConfirmAction();
    }
    public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);

    public System.Reactive.Unit Unit => System.Reactive.Unit.Default;

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> RemovePackagesCommand { get; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> RefreshCommand { get; }

    public ObservableCollection<PackageModel> AvailablePackages { get; set; }

    public IEnumerable<PackageModel> FilteredPackages => _filteredPackages.Value;

    public string? SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }
}