using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.XPath;
using Avalonia.Controls;
using PackageManager.Aur;
using ReactiveUI;
using Shelly_UI.BaseClasses;
using Shelly_UI.Models;
using Shelly_UI.Services;
using Shelly_UI.Services.AppCache;

namespace Shelly_UI.ViewModels.AUR;

public class AurViewModel : ConsoleEnabledViewModelBase, IRoutableViewModel, IActivatableViewModel
{
    public IScreen HostScreen { get; }
    public ViewModelActivator Activator { get; } = new ViewModelActivator();
    private IAurPackageManager _aurManager = new AurPackageManager();
    private IAurSearchManager _aurSearchManager = new AurSearchManager(new HttpClient());
    private readonly IPrivilegedOperationService _privilegedOperationService;
    private string? _searchText;

    private readonly ConfigService _configService = new();

    private IAppCache _appCache;
    private readonly ICredentialManager _credentialManager;

    public AurViewModel(IScreen screen, IAppCache appCache, IPrivilegedOperationService privilegedOperationService,
        ICredentialManager credentialManager)
    {
        HostScreen = screen;

        _appCache = appCache;
        _privilegedOperationService = privilegedOperationService;
        _credentialManager = credentialManager;

        var _ = ConsoleLogService.Instance;

        AlpmInstallCommand = ReactiveCommand.CreateFromTask(AlpmInstall);
        TogglePackageCheckCommand = ReactiveCommand.Create<AurModel>(TogglePackageCheck);
        SearchCommand = ReactiveCommand.CreateFromTask(Search);
        
        SearchedPackages = new ObservableCollection<AurModel>();
    }

    private async Task Search()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            return;

        var result = await _aurSearchManager.SearchAsync(SearchText, cancellationToken: CancellationToken.None);

        Console.WriteLine($"[DEBUG_LOG] Search result: {result.Results.Count}");

        result.Results = result.Results.OrderByDescending(x => x.NumVotes).ToList();
        
        SearchedPackages.Clear();
        foreach (var dto in result.Results)
        {
            SearchedPackages.Add(new AurModel
            {
                Name = dto.Name,
                Description = dto.Description,
                Url = dto.Url,
                Version = dto.Version,
                Popularity = dto.Popularity,
                NumVotes = dto.NumVotes,
                Maintainer = dto.Maintainer,
                FirstSubmitted = DateTimeOffset.FromUnixTimeSeconds(dto?.FirstSubmitted ?? 0).DateTime,
                LastModified =  DateTimeOffset.FromUnixTimeSeconds(dto?.LastModified ?? 0).DateTime
            });
        }
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
        var selectedPackages = SearchedPackages.Where(x => x.IsChecked).Select(x => x.Name).ToList();

        Console.WriteLine($"[DEBUG_LOG] Selected packages: {selectedPackages.Count}");
        
        if (selectedPackages.Any())
        {
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


                // Set busy
                if (mainWindow != null)
                {
                    mainWindow.GlobalProgressValue = 0;
                    mainWindow.GlobalProgressText = "0%";
                    mainWindow.IsGlobalBusy = true;
                    mainWindow.GlobalBusyMessage = "Installing selected packages...";
                }

                //do work
               
                var result = await _privilegedOperationService.InstallAurPackagesAsync(selectedPackages);
                if (!result.Success)
                {
                    Console.WriteLine($"Failed to install packages: {result.Error}");
                }
                
            }
            finally
            {
                //always exit globally busy in case of failure
                if (mainWindow != null)
                {
                    mainWindow.IsGlobalBusy = false;
                }
            }
        }
        else
        {
            ShowConfirmDialog = false;
        }
    }

    private void TogglePackageCheck(AurModel package)
    {
        package.IsChecked = !package.IsChecked;

        Console.Error.WriteLine($"[DEBUG_LOG] Package {package.Name} checked state: {package.IsChecked}");
    }

    public ReactiveCommand<AurModel, Unit> TogglePackageCheckCommand { get; }

    public ReactiveCommand<Unit, Unit> AlpmInstallCommand { get; }
    public ReactiveCommand<Unit, Unit> SearchCommand { get; }

    public ObservableCollection<AurModel> SearchedPackages { get; }

    public string? SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);
}