using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
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

        _ = SetUpdateText();


        CheckForUpdatesCommand = ReactiveCommand.CreateFromTask(CheckForUpdates);
        ForceSyncUpdateCommand = ReactiveCommand.CreateFromTask(ForceSyncUpdate);
    }

    private Color _accentHex;

    private bool _isDarkMode;

    private bool _enableConsole;

    private bool _enableAur;

    private bool _enableFlatpak;
    

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
            new ThemeService().SetTheme(value);

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

    public bool EnableFlatpak
    {
        get => _enableFlatpak;
        set
        {
            this.RaiseAndSetIfChanged(ref _enableFlatpak, value);

            var config = _configService.LoadConfig();
            config.FlatPackEnabled = value;
            _configService.SaveConfig(config);
        }
    }

    public bool EnableAur
    {
        get => _enableAur;
        set
        {
            this.RaiseAndSetIfChanged(ref _enableAur, value);
            
            MessageBus.Current.SendMessage(new AurEnableMessage());
            
            var config = _configService.LoadConfig();
            config.AurEnabled = value;
            _configService.SaveConfig(config);
        }
    }

    public IScreen HostScreen { get; }

    public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);

    public ReactiveCommand<Unit, Unit> CheckForUpdatesCommand { get; }

    public ReactiveCommand<Unit, Unit> ForceSyncUpdateCommand { get; }

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

        bool updateAvailable = await _updateService.CheckForUpdateAsync();
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

    private string _updateAvailable = "Checking for updates...";

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