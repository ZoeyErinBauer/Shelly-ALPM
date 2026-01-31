using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Shelly_UI.Models;
using Shelly_UI.ViewModels.AUR;

namespace Shelly_UI.Views.AUR;

public partial class RemoveAurWindow : ReactiveUserControl<AurRemoveViewModel>
{
    private DataGrid? _dataGrid;
    
    public RemoveAurWindow()
    {
        AvaloniaXamlLoader.Load(this);
        
        this.WhenActivated(disposables =>
        {
            _dataGrid = this.FindControl<DataGrid>("AurRemoveDataGrid"); 
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
        
        if (DataContext is AurRemoveViewModel and IDisposable disposable)
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

        if (row?.DataContext is not AurModel package) return;
        if (DataContext is not AurRemoveViewModel vm) return;


        vm.TogglePackageCheckCommand.Execute(package).Subscribe();
    }
}