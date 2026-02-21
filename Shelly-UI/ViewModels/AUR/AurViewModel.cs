using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using System.Xml.XPath;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Shelly_UI.BaseClasses;
using Shelly_UI.CustomControls.Dialogs;
using Shelly_UI.Models;
using Shelly_UI.Services;
using Shelly_UI.Services.AppCache;

namespace Shelly_UI.ViewModels.AUR;

public class AurViewModel : ConsoleEnabledViewModelBase, IRoutableViewModel, IActivatableViewModel
{
    public IScreen HostScreen { get; }
    public ViewModelActivator Activator { get; } = new ViewModelActivator();
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
        HelpTextVisibility = false;

        if (string.IsNullOrWhiteSpace(SearchText))
            return;

        var result = await _privilegedOperationService.SearchAurPackagesAsync(SearchText);

        Console.WriteLine($"[DEBUG_LOG] Search result: {result.Count}");

        result = result.OrderByDescending(x => x.NumVotes).ToList();

        SearchedPackages.Clear();
        foreach (var dto in result)
        {
            SearchedPackages.Add(new AurModel
            {
                Name = dto.Name,
                Description = dto.Description,
                Url = dto.Url,
                Version = dto.Version,
                Popularity = Math.Round(dto.Popularity, 2, MidpointRounding.ToZero),
                NumVotes = dto.NumVotes,
                Maintainer = dto.Maintainer,
                FirstSubmitted = DateTimeOffset.FromUnixTimeSeconds(dto?.FirstSubmitted ?? 0).DateTime,
                LastModified = DateTimeOffset.FromUnixTimeSeconds(dto?.LastModified ?? 0).DateTime
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

        if (selectedPackages.Count != 0)
        {
            var mainWindow = HostScreen as MainWindowViewModel;
            
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
                    mainWindow.GlobalBytesValue = "";
                    mainWindow.GlobalProgressText = "0%";
                    mainWindow.IsGlobalBusy = true;
                    mainWindow.GlobalBusyMessage = "Installing selected packages...";
                }

                //do work
                if (!App.Services.GetRequiredService<IConfigService>().LoadConfig().NoConfirm)
                {
                    var packageBuilds = await _privilegedOperationService.GetAurPackageBuild(selectedPackages);

                    if (packageBuilds.Count == 0)
                    {
                        Console.WriteLine("No packages found.");
                        return;
                    }

                    foreach (var pkgbuild in packageBuilds)
                    {
                        var window =
                            Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                                ? desktop.MainWindow
                                : null;

                        var dialog = new PackageBuildDialog($"Displaying Package Build {pkgbuild.Name}",
                            packageBuild: pkgbuild.PkgBuild);
                        var dialogResult = await dialog.ShowDialog<bool>(window);
                        if (!dialogResult) return;
                    }
                }
                
                _ = Task.Run(() => { });

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

    private bool _helpTextVisibility = true;

    public bool HelpTextVisibility
    {
        get => _helpTextVisibility;
        set => this.RaiseAndSetIfChanged(ref _helpTextVisibility, value);
    }

    public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SearchedPackages?.Clear();
        }

        base.Dispose(disposing);
    }
}