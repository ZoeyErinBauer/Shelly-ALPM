using System.Reactive.Concurrency;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using ReactiveUI;
using Shelly_UI.BaseClasses;
using Shelly_UI.Enums;
using Shelly_UI.Models;
using Shelly_UI.Services;
using Shelly_UI.Services.AppCache;

namespace Shelly_UI.ViewModels;

public class PackageViewModel : ConsoleEnabledViewModelBase, IRoutableViewModel
{
    public IScreen HostScreen { get; }
    private readonly IPrivilegedOperationService _privilegedOperationService;
    private string? _searchText;
    
    private List<PackageModel> _availablePackages = new();

    private readonly ConfigService _configService = new();
    
    private readonly ICredentialManager _credentialManager;
    public HierarchicalTreeDataGridSource<PackageModel> Source { get; }
    public PackageViewModel(IScreen screen, IPrivilegedOperationService privilegedOperationService,
        ICredentialManager credentialManager)
    {
        HostScreen = screen;
        AvailablePackages = new ObservableCollection<PackageModel>();
        
        _privilegedOperationService = privilegedOperationService;
        _credentialManager = credentialManager;
        
        Source = new HierarchicalTreeDataGridSource<PackageModel>(AvailablePackages)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<PackageModel>(
                    new TextColumn<PackageModel, string>("Name", x => x.Name),
                    x => x.Children,  // Child selector - returns IEnumerable<PackageModel>
                    x => x.HasChildren,  // Has children predicate
                    x => x.IsExpanded  // Is expanded predicate
                ),
                new TextColumn<PackageModel, string>("Version", x => x.Version, width: GridLength.Parse("150")),
                new TextColumn<PackageModel, string>("Size", x => x.SizeString, width: GridLength.Auto),
                new TextColumn<PackageModel, string>("Package Repository", x => x.Repository, width: GridLength.Auto), new TemplateColumn<PackageModel>(
                    "", 
                    new FuncDataTemplate<PackageModel>((model, _) => 
                        new CheckBox 
                        { 
                            [!CheckBox.IsCheckedProperty] = new Binding("IsChecked"),
                            HorizontalAlignment = HorizontalAlignment.Center 
                        }
                    ),
                    width: GridLength.Parse("60")
                )}
               /*new TemplateColumn<PackageModel>(
            "Name",
            new FuncDataTemplate<PackageModel>((model, _) =>
            {
                var panel = new StackPanel 
                { 
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Margin = new Thickness(12, 0)
                };
                
                panel.Children.Add(new TextBlock 
                { 
                    [!TextBlock.TextProperty] = new Binding("Name"),
                    VerticalAlignment = VerticalAlignment.Center
                });
                
                var icon = new Material.Icons.Avalonia.MaterialIcon
                {
                    Kind = Material.Icons.MaterialIconKind.CheckCircle,
                    Foreground = Brushes.Green,
                    Width = 16,
                    Height = 16,
                    [!Visual.IsVisibleProperty] = new Binding("IsInstalled")
                };
                panel.Children.Add(icon);
                
                return panel;
            }),
            width: GridLength.Parse("200")
        ),
                new TextColumn<PackageModel, string>("Version", x => x.Version, width:GridLength.Parse("150")),
                new TextColumn<PackageModel, string>("Size", x => x.SizeString, width:GridLength.Auto),
                new TextColumn<PackageModel, string>("Package Repository", x => x.Repository, width:GridLength.Auto),
                new TemplateColumn<PackageModel>(
                    "", 
                    new FuncDataTemplate<PackageModel>((model, _) => 
                        new CheckBox 
                        { 
                            [!CheckBox.IsCheckedProperty] = new Binding("IsChecked"),
                            HorizontalAlignment = HorizontalAlignment.Center 
                        }
                    ),
                    width: GridLength.Parse("60")
                )
            },*/
        };
        
        var _ = ConsoleLogService.Instance;
        
        this.WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => ApplyFilter());

        AlpmInstallCommand = ReactiveCommand.CreateFromTask(AlpmInstall);
        SyncCommand = ReactiveCommand.CreateFromTask(Sync);
        TogglePackageCheckCommand = ReactiveCommand.Create<PackageModel>(TogglePackageCheck);

        // Load data when the view model is activated (navigated to)
        LoadData();
        
    }

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _availablePackages
            : _availablePackages.Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        AvailablePackages.Clear();
    
        foreach (var package in filtered)
        {
            AvailablePackages.Add(package);
        }
    }

    private async Task Sync()
    {
        try
        {
            var result = await _privilegedOperationService.SyncDatabasesAsync();
            if (!result.Success)
            {
                Console.Error.WriteLine($"Failed to sync databases: {result.Error}");
            }
       
            var installed = await _privilegedOperationService.GetInstalledPackagesAsync();
            
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                _availablePackages.Clear();
                AvailablePackages.Clear();
                LoadData();
            });
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Failed to sync packages: {e.Message}");
        }
    }

    private async void LoadData()
    {
        try
        {
            var packages = await _privilegedOperationService.GetAvailablePackagesAsync();

            var installed = await _privilegedOperationService.GetInstalledPackagesAsync();   
            var installedNames = new HashSet<string>(installed?.Select(x => x.Name) ?? Enumerable.Empty<string>());

            var models = packages.Select(u => new PackageModel
            {
                Name = u.Name,
                Version = u.Version,
                DownloadSize = u.Size,
                Description = u.Description,
                Url = u.Url,
                IsChecked = false,
                IsInstalled = installedNames.Contains(u.Name),
                Repository = u.Repository,
                Children = new ObservableCollection<PackageModel>
                {
                    new PackageModel
                    {
                        Name = $"Description: {u.Description}",
                        Version = null,
                        DownloadSize = 0
                    },
                    new PackageModel
                    {
                        Name = $"URL: {u.Url}",
                        Version = null,
                        DownloadSize = 0
                    }
                }
            }).ToList();
            
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                _availablePackages = models;
                ApplyFilter();
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load available packages: {e.Message}");
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
        var selectedPackages = AvailablePackages.Where(x => x.IsChecked).Select(x => x.Name).ToList();

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
                var result = await _privilegedOperationService.InstallPackagesAsync(selectedPackages);
                if (!result.Success)
                {
                    Console.WriteLine($"Failed to install packages: {result.Error}");
                }

                await Sync();
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
    
    private FlatTreeDataGridSource<PackageModel>? _packageSource;
    public FlatTreeDataGridSource<PackageModel>? PackageSource
    {
        get => _packageSource;
        private set => this.RaiseAndSetIfChanged(ref _packageSource, value);
    }
    
    private void TogglePackageCheck(PackageModel package)
    {
        package.IsChecked = !package.IsChecked;

        Console.Error.WriteLine($"[DEBUG_LOG] Package {package.Name} checked state: {package.IsChecked}");
    }

    public ReactiveCommand<PackageModel, Unit> TogglePackageCheckCommand { get; }

    public ReactiveCommand<Unit, Unit> AlpmInstallCommand { get; }
    public ReactiveCommand<Unit, Unit> SyncCommand { get; }

    public ObservableCollection<PackageModel> AvailablePackages { get; set; }

    public string? SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            AvailablePackages?.Clear();
            _availablePackages?.Clear();
        }
        base.Dispose(disposing);
    }
}