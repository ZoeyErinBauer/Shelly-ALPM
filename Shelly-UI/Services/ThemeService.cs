using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Shelly_UI.Services;

public class ThemeService
{
    public void ApplyCustomAccent(Color accent)
    {
        var fluentTheme = Application.Current?.Styles.OfType<FluentTheme>().FirstOrDefault();
        if (fluentTheme != null)
        {
            if (fluentTheme.Palettes.TryGetValue(ThemeVariant.Dark, out var currentDark) &&
                currentDark is { } darkPalette)
            {
                darkPalette.Accent = accent;
                fluentTheme.Palettes[ThemeVariant.Dark] = darkPalette;
            }

            if (fluentTheme.Palettes.TryGetValue(ThemeVariant.Light, out var currentLight) &&
                currentLight is { } lightPalette)
            {
                lightPalette.Accent = accent;
                fluentTheme.Palettes[ThemeVariant.Light] = lightPalette;
            }
        }
    }

    public void ApplyLowChromeColor(Color accent)
    {
        var fluentTheme = Application.Current?.Styles.OfType<FluentTheme>().FirstOrDefault();
        if (fluentTheme != null)
        {
            if (fluentTheme.Palettes.TryGetValue(ThemeVariant.Dark, out var currentDark) &&
                currentDark is { } darkPalette)
            {
                darkPalette.ChromeLow = accent;
                fluentTheme.Palettes[ThemeVariant.Dark] = darkPalette;
            }

            if (fluentTheme.Palettes.TryGetValue(ThemeVariant.Light, out var currentLight) &&
                currentLight is { } lightPalette)
            {
                lightPalette.ChromeLow = accent;
                fluentTheme.Palettes[ThemeVariant.Light] = lightPalette;
            }
        }
    }

    public void ApplyAltHighColor(Color accent)
    {
        var fluentTheme = Application.Current?.Styles.OfType<FluentTheme>().FirstOrDefault();
        if (fluentTheme != null)
        {
            if (fluentTheme.Palettes.TryGetValue(ThemeVariant.Dark, out var currentDark) &&
                currentDark is { } darkPalette)
            {
                darkPalette.BaseHigh = accent;
                fluentTheme.Palettes[ThemeVariant.Dark] = darkPalette;
            }

            if (fluentTheme.Palettes.TryGetValue(ThemeVariant.Light, out var currentLight) &&
                currentLight is { } lightPalette)
            {
                lightPalette.BaseHigh = accent;
                fluentTheme.Palettes[ThemeVariant.Light] = lightPalette;
            }
        }
        
        //
        Application.Current.Resources["SystemControlForegroundBaseHighBrush"] =
           accent;
    }

    public void ApplySecondaryBackground(Color accent)
    {
        var fluentTheme = Application.Current?.Styles.OfType<FluentTheme>().FirstOrDefault();
        if (fluentTheme != null)
        {
            if (fluentTheme.Palettes.TryGetValue(ThemeVariant.Dark, out var currentDark) &&
                currentDark is { } darkPalette)
            {
                darkPalette.ChromeMedium = accent;
                fluentTheme.Palettes[ThemeVariant.Dark] = darkPalette;
            }

            if (fluentTheme.Palettes.TryGetValue(ThemeVariant.Light, out var currentLight) &&
                currentLight is { } lightPalette)
            {
                lightPalette.ChromeMedium = accent;
                fluentTheme.Palettes[ThemeVariant.Light] = lightPalette;
            }
        }
    }

    public void ApplyKdeTheme()
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "kdeglobals"
        );

        if (!File.Exists(configPath))
            return;

        var content = File.ReadAllText(configPath);
        var parser = new KdeThemeParser();
        parser.Parse(content);

        ApplyLowChromeColor(parser.BaseBackground);
        ApplyCustomAccent(parser.Highlight);
        ApplySecondaryBackground(parser.AlternateBase);
        ApplyAltHighColor(parser.Text);
    }

    public static void SetTheme(bool isDark)
    {
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }
}

public class KdeThemeParser
{
    public Color BaseBackground { get; set; }
    public Color AlternateBase { get; set; }
    public Color Highlight { get; set; }
    public Color Text { get; set; }


    public void Parse(string configContent)
    {
        List<Color> colors = [];


        var lines = configContent.Trim().Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        // Parse section header
        var inWindowSection = false;

        // Parse color entries
        foreach (var line in lines)
        {
            // Check if we're entering the Colors:Window section
            if (line == "[Colors:Window]")
            {
                inWindowSection = true;
                continue;
            }

            // Check if we're entering a different section
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                inWindowSection = false;
                continue;
            }

            if (inWindowSection && line.Contains("="))
            {
                var parts = line.Split('=');
                var name = parts[0].Trim();
                var rgbValues = parts[1].Split(',')
                    .Select(v => int.Parse(v.Trim()))
                    .ToArray();

                colors.Add(Color.FromRgb(Convert.ToByte(rgbValues[0]), Convert.ToByte(rgbValues[1]),
                    Convert.ToByte(rgbValues[2])));
            }
        }

        if (colors.Count > 0)
        {
            BaseBackground = colors[1];
            AlternateBase = colors[0];
            Highlight = colors[2];
            Text = colors[9];
        }
    }
}