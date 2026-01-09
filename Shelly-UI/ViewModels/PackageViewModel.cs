using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using PackageManager.Alpm;
using ReactiveUI;
using Shelly_UI.Models;

namespace Shelly_UI.ViewModels;

public class PackageViewModel : ViewModelBase, IRoutableViewModel
{
    public IScreen HostScreen { get; }
    private AlpmManager _alpmManager = new AlpmManager();
    private string? _searchText;
    private readonly ObservableAsPropertyHelper<IEnumerable<InstallModel>> _filteredPackages;

    public PackageViewModel(IScreen screen)
    {
        HostScreen = screen;
        _alpmManager.IntializeWithSync();
        
        var packages = _alpmManager.GetAvailablePackages();
        
        AvaliablePackages = new ObservableCollection<InstallModel>(
            packages.Select(u => new InstallModel 
            {
                Name = u.Name,
                Version = u.Version,
                DownloadSize = u.Size,
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

    private IEnumerable<InstallModel> Search(string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return AvaliablePackages;
        }

        return AvaliablePackages.Where(p => 
            p.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    
    }
    
    public ObservableCollection<InstallModel> AvaliablePackages { get; set; }

    public IEnumerable<InstallModel> FilteredPackages => _filteredPackages.Value;

    public string? SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }
    
    public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);
    
}
