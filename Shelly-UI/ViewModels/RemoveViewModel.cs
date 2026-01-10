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
    private readonly ObservableAsPropertyHelper<IEnumerable<AlpmPackageDto>> _filteredPackages;

    public RemoveViewModel(IScreen screen)
    {
        HostScreen = screen;
        AvailablePackages = new ObservableCollection<AlpmPackageDto>();

        _filteredPackages = this
            .WhenAnyValue(x => x.SearchText, x => x.AvailablePackages.Count, (s, c) => s)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(Search)
            .ToProperty(this, x => x.FilteredPackages);

        LoadData();
    }

    private async void LoadData()
    {
        try
        {
            await Task.Run(() => _alpmManager.IntializeWithSync());
            var packages = await Task.Run(() => _alpmManager.GetInstalledPackages());

            RxApp.MainThreadScheduler.Schedule(() =>
            {
                foreach (var pkg in packages)
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

    private IEnumerable<AlpmPackageDto> Search(string? searchText)
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

    public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);

    public ObservableCollection<AlpmPackageDto> AvailablePackages { get; set; }

    public IEnumerable<AlpmPackageDto> FilteredPackages => _filteredPackages.Value;

    public string? SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }
}