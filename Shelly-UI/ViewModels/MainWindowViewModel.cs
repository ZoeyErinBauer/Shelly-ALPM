using System.Collections.ObjectModel;
using System.Reactive;
using Shelly_UI.Assets;
using ReactiveUI;
using Material.Icons;

namespace Shelly_UI.ViewModels;

public class MainWindowViewModel : ViewModelBase, IScreen
{
    public MainWindowViewModel()
    {
        GoHome = ReactiveCommand.CreateFromObservable(() => Router.Navigate.Execute(new HomeViewModel(this)));
        GoPackages = ReactiveCommand.CreateFromObservable(() => Router.Navigate.Execute(new PackageViewModel(this)));
        GoUpdate = ReactiveCommand.CreateFromObservable(() => Router.Navigate.Execute(new UpdateViewModel(this)));
        GoRemove = ReactiveCommand.CreateFromObservable(() => Router.Navigate.Execute(new RemoveViewModel(this)));
        GoSetting = ReactiveCommand.CreateFromObservable(() => Router.Navigate.Execute(new SettingViewModel(this)));

        MenuItems = new()
        {
            new MenuItemViewModel( Resources.Home, MaterialIconKind.Home, "Home page", GoHome),
            new MenuItemViewModel(Resources.Packages,MaterialIconKind.PackageVariantClosed,"View New Packages to Install",
                GoPackages),
            new MenuItemViewModel(Resources.Updates, MaterialIconKind.Update, "Update Existing Packages", GoUpdate),
            new MenuItemViewModel(Resources.Remove, MaterialIconKind.Delete, "Delete Existing Packages", GoRemove),
            new MenuItemViewModel(Resources.Settings, MaterialIconKind.Settings, "Application Settings", GoSetting)
        };
        
        GoHome.Execute(Unit.Default);
    }

    public ObservableCollection<MenuItemViewModel> MenuItems { get; } 

    private bool _isPaneOpen = false;

    public bool IsPaneOpen
    {
        get => _isPaneOpen;
        set => this.RaiseAndSetIfChanged(ref _isPaneOpen, value);
    }
    
    public void TogglePane()
    {
        IsPaneOpen = !IsPaneOpen;
    }

    public RoutingState Router { get; } = new RoutingState();

    #region ReactiveCommands

    public static ReactiveCommand<Unit, IRoutableViewModel> GoHome { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoUpdate { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoRemove { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoSetting { get; set; } = null!;

    public static ReactiveCommand<Unit, IRoutableViewModel> GoPackages { get; set; } = null!;

    #endregion
   
    #region MenuItemSelectionNav
    private MenuItemViewModel? _selectedMenuItem;

    public MenuItemViewModel? SelectedMenuItem
    {
        get => _selectedMenuItem;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedMenuItem, value);

            if (value?.Command != null)
            {
                value.Command.Execute(Unit.Default);
            }
        }
    }
    #endregion
}

public class MenuItemViewModel(
    string label,
    MaterialIconKind icon,
    string toolTip,
    ReactiveCommand<Unit, IRoutableViewModel> command)
{
    public string Label { get; } = label;
    public MaterialIconKind Icon { get; } = icon;

    public string ToolTip { get; } = toolTip;

    public ReactiveCommand<Unit, IRoutableViewModel> Command { get; set; } = command;
}