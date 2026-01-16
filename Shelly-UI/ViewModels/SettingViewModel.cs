using System;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using ReactiveUI;
using Shelly_UI.Enums;

using Shelly_UI.Models;
using Shelly_UI.Services;
using Shelly_UI.Services.AppCache;

namespace Shelly_UI.ViewModels;

public class SettingViewModel : ViewModelBase, IRoutableViewModel
{
    private string _selectedTheme;

    private readonly IConfigService _configService;
    
    private readonly IUpdateService _updateService;
    
    private IAppCache _appCache;

    public SettingViewModel(IScreen screen, IConfigService configService, IUpdateService updateService, IAppCache appCache)
    {
        HostScreen = screen;
        _configService = configService;
        _updateService = updateService;
        _appCache = appCache;
        var fluentTheme = Application.Current?.Styles.OfType<FluentTheme>().FirstOrDefault();
        if (fluentTheme != null && fluentTheme.Palettes.TryGetValue(ThemeVariant.Dark, out var dark) && dark is { } pal)
        {
            _accentHex = pal.Accent.ToString();
        }

        var config = _configService.LoadConfig();
        _isDarkMode = config.DarkMode;

        CheckForUpdatesCommand = ReactiveCommand.CreateFromTask(CheckForUpdates);
    }

    private string _accentHex = "#018574";

    private bool _isDarkMode;

    private bool _enableAur;

    private bool _enableFlatpak;

    private bool _enableSnapd;

    public string AccentHex
    {
        get => _accentHex;
        set => this.RaiseAndSetIfChanged(ref _accentHex, value);
    }

    public void ApplyCustomAccent()
    {
        new ThemeService().ApplyCustomAccent(AccentHex);
        var config = _configService.LoadConfig();
        config.AccentColor = AccentHex;
        _configService.SaveConfig(config);
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

    public bool EnableSnap
    {
        get => _enableSnapd;
        set
        {
            this.RaiseAndSetIfChanged(ref _enableSnapd, value);

            var config = _configService.LoadConfig();
            config.SnapEnabled = value;
            _configService.SaveConfig(config);
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

            var config = _configService.LoadConfig();
            config.AurEnabled = value;
            _configService.SaveConfig(config);
        }
    }

    public IScreen HostScreen { get; }

    public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);

    public ReactiveCommand<Unit, Unit> CheckForUpdatesCommand { get; }

    public bool IsUpdateCheckVisible => !AppContext.BaseDirectory.StartsWith("/usr/share/bin/Shelly") && !AppContext.BaseDirectory.StartsWith("/usr/share/Shelly");

    private async Task CheckForUpdates()
    {
        if (AppContext.BaseDirectory.StartsWith("/usr/share/bin/Shelly") || AppContext.BaseDirectory.StartsWith("/usr/share/Shelly"))
        {
            return;
        }
        

        bool updateAvailable = await _updateService.CheckForUpdateAsync();
        if (updateAvailable)
        {
            // Here you might want to show a dialog to the user
            // For now, as per requirement, we proceed with download and install
            await _updateService.DownloadAndInstallUpdateAsync();
        }
    }
}