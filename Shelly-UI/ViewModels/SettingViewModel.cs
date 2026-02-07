using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Shelly_UI.Enums;
using Shelly_UI.Messages;
using Shelly_UI.Services;
using Shelly_UI.Services.AppCache;

namespace Shelly_UI.ViewModels;

public class SettingViewModel : ViewModelBase, IRoutableViewModel
{
    private readonly IConfigService _configService;

    private readonly IUpdateService _updateService;

    private readonly IPrivilegedOperationService _privilegedOperationService;

    private IAppCache _appCache;

    public SettingViewModel(IScreen screen, IConfigService configService, IUpdateService updateService,
        IAppCache appCache, IPrivilegedOperationService privilegedOperationService)
    {
        HostScreen = screen;
        _configService = configService;
        _updateService = updateService;
        _appCache = appCache;
        _privilegedOperationService = privilegedOperationService;
        var fluentTheme = Application.Current?.Styles.OfType<FluentTheme>().FirstOrDefault();
        if (fluentTheme != null && fluentTheme.Palettes.TryGetValue(ThemeVariant.Dark, out var dark) && dark is { } pal)
        {
            _accentHex = pal.Accent;
        }

        var config = _configService.LoadConfig();
        _isDarkMode = config.DarkMode;
        _enableConsole = config.ConsoleEnabled;
        _enableAur = config.AurEnabled;
        _enableFlatpak = config.FlatPackEnabled;
        _useKdeColor = config.UseKdeTheme;
        _useHorizontalMenu = config.UseHorizontalMenu;

        _ = SetUpdateText();
        _ = CheckAndEnableFlatpakAsync();

        CheckForUpdatesCommand = ReactiveCommand.CreateFromTask(CheckForUpdates);
        ForceSyncUpdateCommand = ReactiveCommand.CreateFromTask(ForceSyncUpdate);
        CancelFlatpakDialog = ReactiveCommand.Create(() => { ShowFlatpakDialog = false; });
        InstallFlatpakCommand = ReactiveCommand.CreateFromTask(InstallFlatpakAsync);
        CancelAurDialog = ReactiveCommand.Create(() => { ShowAurDialog = false; });
        ConfirmAurWarningCommand = ReactiveCommand.Create(ConfirmAurWarning);
    }

    private Color _accentHex;

    private bool _isDarkMode;

    private bool _enableConsole;

    private bool _enableAur;

    private bool _enableFlatpak;

    private bool _useHorizontalMenu;

    public Color AccentHex
    {
        get => _accentHex;
        set => this.RaiseAndSetIfChanged(ref _accentHex, value);
    }

    public void ApplyCustomAccent()
    {
        new ThemeService().ApplyCustomAccent(AccentHex);
        var config = _configService.LoadConfig();
        config.AccentColor = AccentHex.ToString();
        _configService.SaveConfig(config);
    }

    private async Task ForceSyncUpdate()
    {
        await _privilegedOperationService.ForceSyncDatabaseAsync();
    }

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _isDarkMode, value);
            ThemeService.SetTheme(value);

            var config = _configService.LoadConfig();
            config.DarkMode = value;
            _configService.SaveConfig(config);
        }
    }

    public bool EnableConsole
    {
        get => _enableConsole;
        set
        {
            this.RaiseAndSetIfChanged(ref _enableConsole, value);

            var config = _configService.LoadConfig();
            config.ConsoleEnabled = value;
            _configService.SaveConfig(config);
            MessageBus.Current.SendMessage(new ConsoleEnableMessage());
        }
    }


    public bool EnableAur
    {
        get => _enableAur;
        set
        {
            var config = _configService.LoadConfig();
            
            // Show warning dialog if enabling AUR for the first time
            if (value && !config.AurWarningConfirmed)
            {
                ShowAurDialog = true;
                
                // Reset toggle to off until user confirms
                Task.Run(async () =>
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _enableAur = false;
                        this.RaisePropertyChanged(nameof(EnableAur));
                    });
                });
                
                return;
            }
            
            this.RaiseAndSetIfChanged(ref _enableAur, value);

            MessageBus.Current.SendMessage(new MainWindowMessage { AurEnable = true });

            config.AurEnabled = value;
            _configService.SaveConfig(config);
        }
    }

    private bool _showAurDialog;

    public bool ShowAurDialog
    {
        get => _showAurDialog;
        set => this.RaiseAndSetIfChanged(ref _showAurDialog, value);
    }

    public ReactiveCommand<Unit, Unit> CancelAurDialog { get; }
    public ReactiveCommand<Unit, Unit> ConfirmAurWarningCommand { get; }

    private void ConfirmAurWarning()
    {
        var config = _configService.LoadConfig();
        config.AurWarningConfirmed = true;
        _configService.SaveConfig(config);
        
        ShowAurDialog = false;
        EnableAur = true;
    }


    private bool _showFlatpakDialog;

    public bool ShowFlatpakDialog
    {
        get => _showFlatpakDialog;
        set => this.RaiseAndSetIfChanged(ref _showFlatpakDialog, value);
    }

    private bool _useKdeColor;

    public bool UseKdeColor
    {
        get => _useKdeColor;
        set
        {
            var sessionDesktop = Environment.GetEnvironmentVariable("XDG_SESSION_DESKTOP");
            if (sessionDesktop != "KDE") return;

            if (!value)
            {
                var fluentTheme = Application.Current?.Styles.OfType<FluentTheme>().FirstOrDefault();
                if (fluentTheme.Palettes.TryGetValue(ThemeVariant.Dark, out var currentDark) &&
                    currentDark is { } darkPalette)
                {
                    var themeService = new ThemeService();
                    themeService.ApplyAltHighColor(Color.FromRgb(255, 255, 255));
                    themeService.ApplyCustomAccent(AccentHex);
                    themeService.ApplyLowChromeColor(Color.FromRgb(23, 23, 23));
                    themeService.ApplySecondaryBackground(Color.FromRgb(31, 31, 31));
                }
                else
                {
                    var themeService = new ThemeService();
                    themeService.ApplyAltHighColor(Color.FromRgb(0, 0, 0));
                    themeService.ApplyCustomAccent(AccentHex);
                    themeService.ApplyLowChromeColor(Color.FromRgb(242, 242, 242));
                    themeService.ApplySecondaryBackground(Color.FromRgb(230, 230, 230));
                }
                var config = _configService.LoadConfig();
                config.UseKdeTheme = value;
                _configService.SaveConfig(config);
            }
            else
            {

                new ThemeService().ApplyKdeTheme();
                var config = _configService.LoadConfig();
                config.UseKdeTheme = value;
                _configService.SaveConfig(config);
            }

            this.RaiseAndSetIfChanged(ref _useKdeColor, value);

        }
    }

    public bool UseHorizontalMenu
    {
        get => _useHorizontalMenu;
        set
        {
            this.RaiseAndSetIfChanged(ref _useHorizontalMenu, value);

            var config = _configService.LoadConfig();
            config.UseHorizontalMenu = value;
            _configService.SaveConfig(config);
            MessageBus.Current.SendMessage(new MainWindowMessage { MenuLayoutChanged = true });
        }
    }

    public bool EnableFlatpak
    {
        get => _enableFlatpak;
        set
        {
            var oldValue = _enableFlatpak;
            this.RaiseAndSetIfChanged(ref _enableFlatpak, value);

            if (value && !IsFlatbackToggleEnabled)
            {
                ShowFlatpakDialog = true;

                Task.Run(async () =>
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _enableFlatpak = false;
                        this.RaisePropertyChanged(nameof(EnableFlatpak));
                    });
                });

                return;
            }

            SendFlatpakMessager();
            if (value == oldValue) return;
            var config = _configService.LoadConfig();
            config.FlatPackEnabled = value;
            _configService.SaveConfig(config);
        }
    }

    private Task SetEnableFlatpakAsync(bool value)
    {
        _enableFlatpak = value;
        this.RaisePropertyChanged(nameof(EnableFlatpak));

        SendFlatpakMessager();

        var config = _configService.LoadConfig();
        config.FlatPackEnabled = value;
        _configService.SaveConfig(config);
        return Task.CompletedTask;
    }

    private void SendFlatpakMessager()
    {
        MessageBus.Current.SendMessage(new MainWindowMessage { FlatpakEnable = true });
    }

    private bool _isFlatbackToggleEnabled = false;

    public bool IsFlatbackToggleEnabled
    {
        get => _isFlatbackToggleEnabled;
        private set => this.RaiseAndSetIfChanged(ref _isFlatbackToggleEnabled, value);
    }

    private async Task CheckAndEnableFlatpakAsync()
    {
        var result = await App.Services.GetService<IPrivilegedOperationService>()?.GetInstalledPackagesAsync()!;
        var flatpakInstalled = result!.Any(x => x.Name == "flatpak");
        IsFlatbackToggleEnabled = flatpakInstalled;
    }


    private async Task<bool> InstallFlatpakAsync()
    {
        using var mainWindow = HostScreen as MainWindowViewModel;
        try
        {
            var credManager = App.Services.GetService<ICredentialManager>();
            if (!credManager!.IsValidated)
            {
                if (!await credManager.RequestCredentialsAsync("Install flatpak") ||
                    string.IsNullOrEmpty(credManager.GetPassword()))
                {
                    ShowFlatpakDialog = false;
                    return false;
                }

                var isValidated = await credManager.ValidateInputCredentials();

                if (!isValidated)
                {
                    ShowFlatpakDialog = false;
                    return false;
                }
            }

            if (mainWindow != null)
            {
                mainWindow.GlobalProgressValue = 0;
                mainWindow.GlobalProgressText = "0%";
                mainWindow.IsGlobalBusy = true;
                mainWindow.GlobalBusyMessage = "Installing flatpak...";
            }

            var result =
                await App.Services.GetService<IPrivilegedOperationService>()?.InstallPackagesAsync(["flatpak"])!;

            if (mainWindow != null) mainWindow.IsGlobalBusy = false;
            ShowFlatpakDialog = false;

            if (!result.Success) return result.Success;

            await CheckAndEnableFlatpakAsync();

            await SetEnableFlatpakAsync(true);

            IsFlatbackToggleEnabled = true;

            return result.Success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Flatpak failed to install: {ex}");
            if (mainWindow != null) mainWindow.IsGlobalBusy = false;
            ShowFlatpakDialog = false;
            return false;
        }
    }

    public IScreen HostScreen { get; }

    public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);

    public ReactiveCommand<Unit, Unit> CheckForUpdatesCommand { get; }

    public ReactiveCommand<Unit, Unit> ForceSyncUpdateCommand { get; }

    public ReactiveCommand<Unit, Unit> CancelFlatpakDialog { get; }

    public ReactiveCommand<Unit, bool> InstallFlatpakCommand { get; }


    public bool IsUpdateCheckVisible => !AppContext.BaseDirectory.StartsWith("/usr/share/bin/Shelly") ||
                                        !AppContext.BaseDirectory.StartsWith("/usr/share/Shelly") ||
                                        !AppContext.BaseDirectory.StartsWith("/usr/bin/Shelly");

    private async Task CheckForUpdates()
    {
#if !DEBUG
        if (AppContext.BaseDirectory.StartsWith("/usr/share/bin/Shelly") || AppContext.BaseDirectory.StartsWith("/usr/share/Shelly") || AppContext.BaseDirectory.StartsWith("/usr/bin/Shelly"))
        {
            return;
        }
#endif

        UpdateAvailableText = "Checking for updates...";
        bool updateAvailable = await _updateService.CheckForUpdateAsync();
        await _appCache.StoreAsync(nameof(CacheEnums.UpdateAvailableCache), updateAvailable);
        await SetUpdateText();
        
        if (updateAvailable)
        {
            // Here you might want to show a dialog to the user
            // For now, as per requirement, we proceed with download and install
            await _updateService.DownloadAndInstallUpdateAsync();
        }
    }

    private async Task SetUpdateText()
    {
        UpdateAvailableText = await _appCache.GetAsync<bool>(nameof(CacheEnums.UpdateAvailableCache))
            ? "Update Available Click to Download"
            : "No Update Available";
    }

    private string _updateAvailable = "Check for update";

    public string UpdateAvailableText
    {
        get => _updateAvailable;
        set => this.RaiseAndSetIfChanged(ref _updateAvailable, value);
    }

    public string AppVersion { get; } = GetAppVersion();

    private static string GetAppVersion()
    {
        var asm = Assembly.GetEntryAssembly() ?? typeof(SettingViewModel).Assembly;
        var nameVer = asm.GetName().Version?.ToString();
        return !string.IsNullOrWhiteSpace(nameVer) ? $"v{nameVer}" : "unknown";
    }

    public IEnumerable<DefaultViewEnum> DefaultViews { get; } =
        Enum.GetValues<DefaultViewEnum>();

    private DefaultViewEnum _defaultScreenEnum;

    public DefaultViewEnum DefaultScreenEnum
    {
        get => _configService.LoadConfig().DefaultView;
        set
        {
            this.RaiseAndSetIfChanged(ref _defaultScreenEnum, value);
            var config = _configService.LoadConfig();
            config.DefaultView = value;
            _configService.SaveConfig(config);
        }
    }
}