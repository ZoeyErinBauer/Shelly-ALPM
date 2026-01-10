using System.Reactive.Concurrency;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using PackageManager.Alpm;
using ReactiveUI;
using Shelly_UI.Models;
using Shelly_UI.Services;

namespace Shelly_UI.ViewModels;

public class PackageViewModel : ViewModelBase, IRoutableViewModel
{
    public IScreen HostScreen { get; }
    private IAlpmManager _alpmManager = AlpmService.Instance;
    private string? _searchText;
    private readonly ObservableAsPropertyHelper<IEnumerable<InstallModel>> _filteredPackages;

    public PackageViewModel(IScreen screen)
    {
        HostScreen = screen;
        AvaliablePackages = new ObservableCollection<InstallModel>();

        _filteredPackages = this
            .WhenAnyValue(x => x.SearchText, x => x.AvaliablePackages.Count, (s, c) => s)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(Search)
            .ToProperty(this, x => x.FilteredPackages);

        AlpmInstallCommand = ReactiveCommand.CreateFromTask(AlpmInstall);
        
        LoadData();
    }

    private async void LoadData()
    {
        try
        {
            await Task.Run(() => _alpmManager.IntializeWithSync());
            var packages = await Task.Run(() => _alpmManager.GetAvailablePackages());

            var models = packages.Select(u => new InstallModel
            {
                Name = u.Name,
                Version = u.Version,
                DownloadSize = u.Size,
                IsChecked = false
            }).ToList();

            RxApp.MainThreadScheduler.Schedule(() =>
            {
                foreach (var model in models)
                {
                    AvaliablePackages.Add(model);
                }
                this.RaisePropertyChanged(nameof(AvaliablePackages));
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load available packages: {e.Message}");
        }
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

    private async Task AlpmInstall()
    {
        var selectedPackages = AvaliablePackages.Where(x => x.IsChecked).Select(x => x.Name).ToList();
        if (selectedPackages.Any())
        {
            _alpmManager.InstallPackages(selectedPackages);
        }

        ToggleConfirmAction();
    }

    public ReactiveCommand<Unit, Unit> AlpmInstallCommand { get; }

    public ObservableCollection<InstallModel> AvaliablePackages { get; set; }

    public IEnumerable<InstallModel> FilteredPackages => _filteredPackages.Value;

    public string? SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);
}