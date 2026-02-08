using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using ReactiveUI;
using Shelly_UI.BaseClasses;
using Shelly_UI.Models;
using Shelly_UI.Services;

namespace Shelly_UI.ViewModels;

public class MetaSearchViewModel : ConsoleEnabledViewModelBase, IRoutableViewModel
{
    public string? UrlPathSegment { get; } = Guid.NewGuid().ToString()[..5];
    public IScreen HostScreen { get; }

    private readonly IPrivilegedOperationService _privilegedOperationService;
    private readonly ICredentialManager _credentialManager;

    private List<PackageModel> _allPackages = new();

    private string? _searchText;

    public string? SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    private ObservableCollection<PackageModel> _availablePackages = new();

    public ObservableCollection<PackageModel> AvailablePackages
    {
        get => _availablePackages;
        set => this.RaiseAndSetIfChanged(ref _availablePackages, value);
    }

    public MetaSearchViewModel(IScreen screen, IPrivilegedOperationService privilegedOperationService,
        ICredentialManager credentialManager)
    {
        HostScreen = screen;
        _privilegedOperationService = privilegedOperationService;
        _credentialManager = credentialManager;

        this.WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => ApplyFilter());

        LoadData();
    }

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allPackages
            : _allPackages.Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        AvailablePackages.Clear();

        foreach (var package in filtered)
        {
            AvailablePackages.Add(package);
        }
    }

    private async void LoadData()
    {
        try
        {
            var available = await _privilegedOperationService.GetAvailablePackagesAsync();
            var installed = await _privilegedOperationService.GetInstalledPackagesAsync();
            var installedNames = new HashSet<string>(installed?.Select(x => x.Name) ?? Enumerable.Empty<string>());

            var models = available.Select(u => new PackageModel
            {
                Name = u.Name,
                Version = u.Version,
                DownloadSize = u.Size,
                Description = u.Description,
                Url = u.Url,
                IsChecked = false,
                IsInstalled = installedNames.Contains(u.Name),
                Repository = u.Repository
            }).ToList();

            RxApp.MainThreadScheduler.Schedule(() =>
            {
                _allPackages = models;
                ApplyFilter();
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load packages: {e.Message}");
        }
    }
}
