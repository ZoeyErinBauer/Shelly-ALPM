using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using ReactiveUI;
using Shelly_UI.Models;
using Shelly_UI.Services;

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

}