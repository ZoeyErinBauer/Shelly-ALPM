using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Shelly_UI.Services;

public class ThemeService
{
    public void ApplyCustomAccent(string accentHex)
    {
        if (Color.TryParse(accentHex, out var newColor))
        {
            var fluentTheme = Application.Current?.Styles.OfType<FluentTheme>().FirstOrDefault();
            if (fluentTheme != null)
            {
                if (fluentTheme.Palettes.TryGetValue(ThemeVariant.Dark, out var currentDark) && currentDark is { } darkPalette)
                {
                    darkPalette.Accent = newColor;
                    fluentTheme.Palettes[ThemeVariant.Dark] = darkPalette;
                }
                
                if (fluentTheme.Palettes.TryGetValue(ThemeVariant.Light, out var currentLight) && currentLight is { } lightPalette)
                {
                    lightPalette.Accent = newColor;
                    fluentTheme.Palettes[ThemeVariant.Light] = lightPalette;
                }
            }
        }
    }
}