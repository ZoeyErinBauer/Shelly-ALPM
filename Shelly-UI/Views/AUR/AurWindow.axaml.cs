using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using ReactiveUI.Avalonia;
using Shelly_UI.Models;
using Shelly_UI.ViewModels.AUR;

namespace Shelly_UI.Views.AUR;

public partial class AurWindow : ReactiveUserControl<AurViewModel>
{
    public AurWindow()
    {
      
        AvaloniaXamlLoader.Load(this);
    }

    private void OpenUrl_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Avalonia.Controls.MenuItem mi)
        {
            var url = mi.CommandParameter as string;
            if (string.IsNullOrWhiteSpace(url)) return;
            OpenUrlCrossPlatform(url!);
        }
    }

    private static void OpenUrlCrossPlatform(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "xdg-open",
                Arguments = url,
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
    
    private void DataGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        var row = (e.Source as Visual)?.FindAncestorOfType<DataGridRow>();

        if (row?.DataContext is not AurModel package) return;
        if (DataContext is not AurViewModel vm) return;


        vm.TogglePackageCheckCommand.Execute(package).Subscribe();
    }
}