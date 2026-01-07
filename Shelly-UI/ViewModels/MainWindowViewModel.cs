using System.Collections.ObjectModel;
using ReactiveUI;
using Material.Icons;

namespace Shelly_UI.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public string Greeting { get; } = "Welcome to Shelly!";

    private bool _isPaneOpen = true;
    public bool IsPaneOpen
    {
        get => _isPaneOpen;
        set => this.RaiseAndSetIfChanged(ref _isPaneOpen, value);
    }

    public ObservableCollection<MenuItemViewModel> MenuItems { get; } = new()
    {
        new MenuItemViewModel("Home", MaterialIconKind.Home),
        new MenuItemViewModel("Packages", MaterialIconKind.PackageVariantClosed),
        new MenuItemViewModel("Updates", MaterialIconKind.Update),
        new MenuItemViewModel("Settings", MaterialIconKind.Settings)
    };

    public void TogglePane()
    {
        IsPaneOpen = !IsPaneOpen;
    }
}

public class MenuItemViewModel(string label, MaterialIconKind icon)
{
    public string Label { get; } = label;
    public MaterialIconKind Icon { get; } = icon;
}