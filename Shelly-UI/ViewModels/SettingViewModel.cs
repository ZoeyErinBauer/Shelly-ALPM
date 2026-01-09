using System;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using ReactiveUI;
using Shelly_UI.Models;
using Shelly_UI.Services;
using Velopack;
using Velopack.Sources;

namespace Shelly_UI.ViewModels;

public class SettingViewModel : ViewModelBase,  IRoutableViewModel
{
    private string _selectedTheme;

    public SettingViewModel(IScreen screen)
    {
        HostScreen = screen;
        var fluentTheme = Application.Current?.Styles.OfType<FluentTheme>().FirstOrDefault();
        if (fluentTheme != null && fluentTheme.Palettes.TryGetValue(ThemeVariant.Dark, out var dark) && dark is { } pal)
        {
            _accentHex = pal.Accent.ToString();
        }

        CheckForUpdatesCommand = ReactiveCommand.CreateFromTask(CheckForUpdates);
    }
    
    private string _accentHex = "#018574";

    public string AccentHex
    {
        get => _accentHex;
        set => this.RaiseAndSetIfChanged(ref _accentHex, value);
    }

    public void ApplyCustomAccent()
    {
       new ThemeService().ApplyCustomAccent(AccentHex);
       new ConfigService().SaveConfig(new ShellyConfig
       {
           AccentColor = AccentHex
       });
    }
    
    public IScreen HostScreen { get; }
    
    public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);

    public ReactiveCommand<Unit, Unit> CheckForUpdatesCommand { get; }
    
    public bool IsUpdateCheckVisible => !AppContext.BaseDirectory.StartsWith("/usr/share");

    private async Task CheckForUpdates()
    {
        if (AppContext.BaseDirectory.StartsWith("/usr/share"))
        {
            return;
        }
        
        var mgr = new UpdateManager(new GithubSource("https://github.com/ZoeyErinBauer/Shelly-ALPM", null, false));

        var newVersion = await mgr.CheckForUpdatesAsync();
        if (newVersion == null)
        {
            return;
        }

        await mgr.DownloadUpdatesAsync(newVersion);
        mgr.ApplyUpdatesAndRestart(newVersion);
    }

}