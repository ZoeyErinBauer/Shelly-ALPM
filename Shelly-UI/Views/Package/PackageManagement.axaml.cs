using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Shelly_UI.Models;
using Shelly_UI.ViewModels;

namespace Shelly_UI.Views;

public partial class PackageManagement : ReactiveUserControl<PackageManagementViewModel>
{
    private DataGrid? _dataGrid;
    
    public PackageManagement()
    {
        AvaloniaXamlLoader.Load(this);
        this.WhenActivated(disposables =>
        {
            _dataGrid = this.FindControl<DataGrid>("RemoveDataGrid"); // Use your actual DataGrid name
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
        
        if (DataContext is PackageManagementViewModel and IDisposable disposable)
        {
            disposable.Dispose();
        }
        
        DataContext = null;
        
        this.DetachedFromVisualTree -= OnDetachedFromVisualTree;
       
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
    
    private void DataGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        var row = (e.Source as Visual)?.FindAncestorOfType<DataGridRow>();

        if (row?.DataContext is not PackageModel package) return;
        if (DataContext is not PackageManagementViewModel vm) return;


        vm.TogglePackageCheckCommand.Execute(package).Subscribe();
    }
}