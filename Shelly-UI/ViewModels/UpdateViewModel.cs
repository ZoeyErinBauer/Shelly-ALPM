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

public class UpdateViewModel : ViewModelBase, IRoutableViewModel
{
    public IScreen HostScreen { get; }
    private IAlpmManager _alpmManager = AlpmService.Instance;
    private string? _searchText;
    private readonly ObservableAsPropertyHelper<IEnumerable<UpdateModel>> _filteredPackages;

    public UpdateViewModel(IScreen screen)
    {
        HostScreen = screen;
        PackagesForUpdating = new ObservableCollection<UpdateModel>();

        _filteredPackages = this
            .WhenAnyValue(x => x.SearchText, x => x.PackagesForUpdating.Count, (s, c) => s)
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
            //await Task.Run(() => _alpmManager.IntializeWithSync());
            var updates = await Task.Run(() => AlpmService.Instance.GetPackagesNeedingUpdate());

            var models = updates.Select(u => new UpdateModel
            {
                Name = u.Name,
                CurrentVersion = u.CurrentVersion,
                NewVersion = u.NewVersion,
                DownloadSize = u.DownloadSize,
                IsChecked = false
            }).ToList();

            RxApp.MainThreadScheduler.Schedule(() =>
            {
                foreach (var model in models)
                {
                    PackagesForUpdating.Add(model);
                }

                this.RaisePropertyChanged(nameof(PackagesForUpdating));
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load package updates: {e.Message}");
        }
    }

    private IEnumerable<UpdateModel> Search(string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return PackagesForUpdating;
        }

        return PackagesForUpdating.Where(p =>
            p.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }

    public ObservableCollection<AlpmPackageDto> AvailablePackages { get; set; }

    public IEnumerable<UpdateModel> FilteredPackages => _filteredPackages.Value;

    public string? SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    public void CheckAll()
    {
        var targetState = PackagesForUpdating.Any(x => !x.IsChecked);

        foreach (var item in PackagesForUpdating)
        {
            item.IsChecked = targetState;
        }
    }

    public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);

    public ObservableCollection<UpdateModel> PackagesForUpdating { get; set; }
}