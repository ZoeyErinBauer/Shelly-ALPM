using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Shelly_UI.ViewModels;

namespace Shelly_UI.CustomControls.Menus;

public partial class VerticalMenu : UserControl
{
    public VerticalMenu()
    {
        InitializeComponent();
    }
    
    private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainWindowViewModel vm)
        {
            vm.GoMetaSearch.Execute().Subscribe();
        }
    }
}
