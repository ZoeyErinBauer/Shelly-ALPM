using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Shelly_UI.ViewModels;
using System.Diagnostics;
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Shelly_UI.Models;

namespace Shelly_UI.Views;

public partial class PackageWindow : ReactiveUserControl<PackageViewModel>
{
    private DataGrid? _dataGrid;
    
    public PackageWindow()
    {
        AvaloniaXamlLoader.Load(this);
        
        this.WhenActivated(disposables =>
        {
            _dataGrid = this.FindControl<DataGrid>("PackageDataGrid"); 
        });
        
        this.DetachedFromVisualTree += OnDetachedFromVisualTree;
    }
    
    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_dataGrid != null)
        {
            _dataGrid.ItemsSource = null;
            _dataGrid = null;
        }
        
        if (DataContext is PackageViewModel and IDisposable disposable)
        {
            disposable.Dispose();
        }
        
        DataContext = null;
     
        this.DetachedFromVisualTree -= OnDetachedFromVisualTree;
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
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

        if (row?.DataContext is not PackageModel package) return;
        if (DataContext is not PackageViewModel vm) return;
        
        vm.TogglePackageCheckCommand.Execute(package).Subscribe();
    }
}