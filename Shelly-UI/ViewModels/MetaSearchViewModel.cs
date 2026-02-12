using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Shelly_UI.BaseClasses;
using Shelly_UI.Models;
using Shelly_UI.Services;
using Shelly_UI.Services.LocalDatabase;

namespace Shelly_UI.ViewModels;

public class MetaSearchViewModel : ConsoleEnabledViewModelBase, IRoutableViewModel
{
    public string? UrlPathSegment { get; } = Guid.NewGuid().ToString()[..5];
    public IScreen HostScreen { get; }

    private readonly IPrivilegedOperationService _privilegedOperationService;
    private readonly ICredentialManager _credentialManager;
    private readonly IUnprivilegedOperationService _unprivilegedOperationService;
    private readonly IDatabaseService _databaseService;
    private readonly IConfigService _configService;

    private List<PackageModel> _allPackages = new();

    private string? _searchText;

    public string? SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    private ObservableCollection<MetaPackageModel> _availablePackages = new();

    public ObservableCollection<MetaPackageModel> AvailablePackages
    {
        get => _availablePackages;
        set => this.RaiseAndSetIfChanged(ref _availablePackages, value);
    }

    private bool _showConfirmDialog;

    public bool ShowConfirmDialog
    {
        get => _showConfirmDialog;
        set => this.RaiseAndSetIfChanged(ref _showConfirmDialog, value);
    }

    public ReactiveCommand<Unit, Unit> InstallCommand { get; }

    public MetaSearchViewModel(IScreen screen)
    {
        HostScreen = screen;
        _privilegedOperationService = App.Services.GetRequiredService<IPrivilegedOperationService>();
        _credentialManager = App.Services.GetRequiredService<ICredentialManager>();
        _unprivilegedOperationService = App.Services.GetRequiredService<IUnprivilegedOperationService>();
        _databaseService = App.Services.GetRequiredService<IDatabaseService>();
        _configService = App.Services.GetRequiredService<IConfigService>();

        InstallCommand = ReactiveCommand.CreateFromTask(InstallSelectedPackages);

        LoadData();

        this.WhenAnyValue(x => x.SearchText)
            .Skip(1)
            .Throttle(TimeSpan.FromMilliseconds(400))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => LoadData());
    }

    public void ToggleConfirmAction()
    {
        ShowConfirmDialog = !ShowConfirmDialog;
    }

    private async Task InstallSelectedPackages()
    {
        var selected = AvailablePackages.Where(x => x.IsChecked).ToList();

        if (!selected.Any())
        {
            ShowConfirmDialog = false;
            return;
        }

        MainWindowViewModel? mainWindow = HostScreen as MainWindowViewModel;

        try
        {
            ShowConfirmDialog = false;

            if (!_credentialManager.IsValidated)
            {
                if (!await _credentialManager.RequestCredentialsAsync("Install Packages")) return;

                if (string.IsNullOrEmpty(_credentialManager.GetPassword())) return;

                var isValidated = await _credentialManager.ValidateInputCredentials();

                if (!isValidated) return;
            }

            if (mainWindow != null)
            {
                mainWindow.GlobalProgressValue = 0;
                mainWindow.GlobalProgressText = "0%";
                mainWindow.IsGlobalBusy = true;
                mainWindow.GlobalBusyMessage = "Installing selected packages...";
            }

            var standardPackages = selected.Where(x => x.PackageType == PackageType.STANDARD).Select(x => x.Name).ToList();
            var aurPackages = selected.Where(x => x.PackageType == PackageType.AUR).Select(x => x.Name).ToList();
            var flatpakPackages = selected.Where(x => x.PackageType == PackageType.FLATPAK).Select(x => x.Id).ToList();

            if (standardPackages.Count != 0)
            {
                var result = await _privilegedOperationService.InstallPackagesAsync(standardPackages);
                if (!result.Success)
                {
                    Console.WriteLine($"Failed to install standard packages: {result.Error}");
                    var err = Logs.FirstOrDefault(x => x.Contains("[ALPM_ERROR]"));
                    mainWindow?.ShowToast($"Standard installation failed: {err}", isSuccess: false);
                }
                else
                {
                    mainWindow?.ShowToast($"Successfully installed {standardPackages.Count} standard package{(standardPackages.Count > 1 ? "s" : "")}");
                }
            }

            if (aurPackages.Count != 0)
            {
                var result = await _privilegedOperationService.InstallAurPackagesAsync(aurPackages);
                if (!result.Success)
                {
                    Console.WriteLine($"Failed to install AUR packages: {result.Error}");
                    var err = Logs.FirstOrDefault(x => x.Contains("[ALPM_ERROR]"));
                    mainWindow?.ShowToast($"AUR installation failed: {err}", isSuccess: false);
                }
                else
                {
                    mainWindow?.ShowToast($"Successfully installed {aurPackages.Count} AUR package{(aurPackages.Count > 1 ? "s" : "")}");
                }
            }

            if (flatpakPackages.Count != 0)
            {
                foreach (var package in flatpakPackages)
                {
                    var result = await _unprivilegedOperationService.InstallFlatpakPackage(package);
                    if (!result.Success)
                    {
                        Console.WriteLine($"Failed to install Flatpak package {package}: {result.Error}");
                        mainWindow?.ShowToast($"Flatpak installation failed: {result.Error}", isSuccess: false);
                    }
                    else
                    {
                        mainWindow?.ShowToast($"Successfully installed Flatpak package {package}");
                    }
                }
            }

            LoadData();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to install packages: {e.Message}");
            var err = Logs.FirstOrDefault(x => x.Contains("[ALPM_ERROR]"));
            mainWindow?.ShowToast($"Installation failed: {err}", isSuccess: false);
        }
        finally
        {
            if (mainWindow != null)
            {
                mainWindow.IsGlobalBusy = false;
            }
        }
    }

    private async void LoadData()
    {
        try
        {
            if (!_databaseService.CollectionExists<FlatpakModel>("flatpaks"))
            {
                await Refresh();
            }

            RxApp.MainThreadScheduler.Schedule(() =>
            {
                AvailablePackages = new ObservableCollection<MetaPackageModel>();
            });
            List<Task<List<MetaPackageModel>>> groupList = [];

            var standardTask = Task.Run(async () =>
            {
                var standardInstalled = await _privilegedOperationService.GetInstalledPackagesAsync().ContinueWith(x =>
                    x.Result.Select(y => new MetaPackageModel(y.Name, y.Name, y.Version, y.Description,
                        PackageType.STANDARD, y.Description, y.Repository, true)).ToList());
                var standardAvailable = await _privilegedOperationService.SearchPackagesAsync(SearchText ?? "")
                    .ContinueWith(x =>
                        x.Result.Select(y => new MetaPackageModel(y.Name, y.Name, y.Version, y.Description,
                            PackageType.STANDARD, y.Description, y.Repository,
                            standardInstalled.Any(z => z.Name == y.Name))).ToList());
                return standardAvailable;
            });
            groupList.Add(standardTask);

            Task<List<MetaPackageModel>>? flatpakGroup = null;
            if (_configService.LoadConfig().FlatPackEnabled)
            {
                flatpakGroup = Task.Run(async () =>
                {
                    var flatPakInstalled = await _unprivilegedOperationService.ListFlatpakPackages().ContinueWith(x =>
                        x.Result.Select(y => new MetaPackageModel(y.Id, y.Name, y.Version, y.Description,
                            PackageType.FLATPAK, y.Summary, "Flathub", true)).ToList());
                    var flatPakAvailable = _databaseService.GetCollection<FlatpakModel>("flatpaks")
                        .Where(x => x.Name.Contains(SearchText ?? "")).Select(y =>
                            new MetaPackageModel(y.Id, y.Name, y.Version, y.Description, PackageType.FLATPAK, y.Summary,
                                "Flathub", flatPakInstalled.Any(z => z.Name == y.Name))).ToList();
                    return flatPakAvailable;
                });
                groupList.Add(flatpakGroup);
            }

            Task<List<MetaPackageModel>>? aurGroup = null;
            if (_configService.LoadConfig().AurEnabled)
            {
                aurGroup = Task.Run(async () =>
                {
                    var aurInstalled = await _privilegedOperationService.GetAurInstalledPackagesAsync()
                        .ContinueWith(x =>
                            x.Result.Select(y => new MetaPackageModel(y.Name, y.Name, y.Version, y.Description ?? "",
                                PackageType.AUR, y.Url ?? "", "AUR", true)).ToList());
                    var aurAvailable = await _privilegedOperationService.SearchAurPackagesAsync(SearchText ?? "")
                        .ContinueWith(x => x.Result.Select(y =>
                            new MetaPackageModel(y.Name, y.Name, y.Version, y.Description ?? "", PackageType.AUR,
                                y.Url ?? "", "AUR", aurInstalled.Any(z => z.Name == y.Name))).ToList());
                    return aurAvailable;
                });
                groupList.Add(aurGroup);
            }

            List<MetaPackageModel> models = [];
            await foreach (var completedTask in Task.WhenEach(groupList))
            {
                var metaEnumerable = await completedTask;
                if (metaEnumerable.Count != 0)
                {
                    models.AddRange(metaEnumerable.ToList());
                }
            }

            RxApp.MainThreadScheduler.Schedule(() =>
            {
                AvailablePackages = new ObservableCollection<MetaPackageModel>(models);
            });
        }

        catch (Exception e)
        {
            Console.WriteLine($"Failed to load packages: {e.Message}");
        }
    }

    private async Task Refresh()
    {
        try
        {
            await _databaseService.EnsureIndex<FlatpakModel>("flatpaks", x => x.Name, x => x.Categories);
            var available = await _unprivilegedOperationService.ListAppstreamFlatpak();

            var models = available.Select(u => new FlatpakModel
            {
                Name = u.Name,
                Version = u.Version,
                Summary = u.Summary,
                IconPath = $"/var/lib/flatpak/appstream/flathub/x86_64/active/icons/64x64/{u.Id}.png",
                Id = u.Id,
                Categories = u.Categories,
                Kind = u.Kind == 0
                    ? "App"
                    : "Runtime",
            }).ToList();
            await new DatabaseService().AddToDatabase(models.ToList(), "flatpaks");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to refresh installed packages: {e.Message}");
        }
    }
}
