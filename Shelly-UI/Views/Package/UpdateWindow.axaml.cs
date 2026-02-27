using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Shelly_UI.Models;
using Shelly_UI.ViewModels.Packages;

namespace Shelly_UI.Views;

public partial class UpdateWindow : ReactiveUserControl<UpdateViewModel>
{
    private DataGrid? _dataGrid;
    public UpdateWindow()
    {
        AvaloniaXamlLoader.Load(this);
        
        this.WhenActivated(disposables =>
        {
            _dataGrid = this.FindControl<DataGrid>("UpdateDataGrid");
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
        
        if (DataContext is UpdateViewModel and IDisposable disposable)
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

        if (row?.DataContext is not UpdateModel package) return;
        if (DataContext is not UpdateViewModel vm) return;


        vm.TogglePackageCheckCommand.Execute(package).Subscribe();
    }
}