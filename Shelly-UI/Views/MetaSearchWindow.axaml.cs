using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Shelly_UI.ViewModels;
using Shelly_UI.ViewModels.Packages;

namespace Shelly_UI.Views;

public partial class MetaSearchWindow : ReactiveUserControl<MetaSearchViewModel>
{
    private DataGrid? _dataGrid;
    
    public MetaSearchWindow()
    {
        AvaloniaXamlLoader.Load(this);
        this.WhenActivated(disposables =>
        {
            _dataGrid = this.FindControl<DataGrid>("MetaSearchDataGrid"); // Use your actual DataGrid name
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
}