using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using PackageManager.Alpm;
using ReactiveUI;
using Shelly_UI.Models;

namespace Shelly_UI.ViewModels;

public class UpdateViewModel : ViewModelBase, IRoutableViewModel
{
    public IScreen HostScreen { get; }
    private AlpmManager _alpmManager = new AlpmManager();
    private string? _searchText;
    private readonly ObservableAsPropertyHelper<IEnumerable<UpdateModel>> _filteredPackages;

    public UpdateViewModel(IScreen screen)
    {
        HostScreen = screen;
        _alpmManager.IntializeWithSync();
        
        var updates = _alpmManager.GetPackagesNeedingUpdate();

        
        PackagesForUpdating = new ObservableCollection<UpdateModel>(
            updates.Select(u => new UpdateModel 
            {
                Name = u.Name,
                CurrentVersion = u.CurrentVersion,
                NewVersion = u.NewVersion,
                DownloadSize = u.DownloadSize,
                IsChecked = false
            })
        );
        
        _filteredPackages = this
            .WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(Search)
            .ToProperty(this, x => x.FilteredPackages);
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
    
    public ObservableCollection<AlpmPackage> AvailablePackages { get; set; }

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