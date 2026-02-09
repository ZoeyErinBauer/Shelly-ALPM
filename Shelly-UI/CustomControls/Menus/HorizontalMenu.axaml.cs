using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Material.Icons.Avalonia;
using ReactiveUI;
using Shelly_UI.ViewModels;
namespace Shelly_UI.CustomControls.Menus;
public partial class HorizontalMenu : UserControl
{
    public HorizontalMenu()
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
