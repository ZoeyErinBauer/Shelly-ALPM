using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Shelly_UI.Services;
using Shelly_UI.ViewModels;

namespace Shelly_UI.Views;

public partial class SettingWindow : ReactiveUserControl<SettingViewModel>
{
    public SettingWindow()
    {
        this.WhenActivated(disposables => { });
        AvaloniaXamlLoader.Load(this);
    }
    
    private void OnFlatpakPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is not ToggleSwitch || DataContext is not SettingViewModel viewModel) return;
        var currentValue = viewModel.EnableFlatpak;
        var wouldBeNewValue = !currentValue;

        if (!wouldBeNewValue || viewModel.IsFlatbackToggleEnabled) return;
        e.Handled = true;
                
        viewModel.ShowFlatpakDialog = true;
    }
    
    private void OnAurPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is not ToggleSwitch || DataContext is not SettingViewModel viewModel) return;
        var currentValue = viewModel.EnableAur;
        var wouldBeNewValue = !currentValue;

        if (!wouldBeNewValue || viewModel.IsAurToggleEnabled) return;
        e.Handled = true;
                
        viewModel.ShowAurDialog = true;
    }

    private void OpenUrlCrossPlatform(object? sender, RoutedEventArgs routedEventArgs)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "xdg-open",
                Arguments = "https://buymeacoffee.com/zoeyerinba3",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to open URL: {ex.Message}");
        }
    }
    
    private void OpenGithub(object? sender, RoutedEventArgs routedEventArgs)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "xdg-open",
                Arguments = "https://github.com/ZoeyErinBauer/Shelly-ALPM",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to open URL: {ex.Message}");
        }
    }
}