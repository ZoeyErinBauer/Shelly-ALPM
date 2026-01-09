using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using PackageManager.Alpm;
using ReactiveUI;
using Shelly_UI.Models;

namespace Shelly_UI.ViewModels;

public class RemoveViewModel : ViewModelBase, IRoutableViewModel
{
    public IScreen HostScreen { get; }
    private AlpmManager _alpmManager = new AlpmManager();
    private string? _searchText;
    private readonly ObservableAsPropertyHelper<IEnumerable<AlpmPackage>> _filteredPackages;

    public RemoveViewModel(IScreen screen)
    {
        HostScreen = screen;
        _alpmManager.IntializeWithSync();
        AvailablePackages = new ObservableCollection<AlpmPackage>(_alpmManager.GetInstalledPackages());

        _filteredPackages = this
            .WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(Search)
            .ToProperty(this, x => x.FilteredPackages);
    }

    private IEnumerable<AlpmPackage> Search(string? searchText)
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

    public ObservableCollection<AlpmPackage> AvailablePackages { get; set; }

    public IEnumerable<AlpmPackage> FilteredPackages => _filteredPackages.Value;

    public string? SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }
}