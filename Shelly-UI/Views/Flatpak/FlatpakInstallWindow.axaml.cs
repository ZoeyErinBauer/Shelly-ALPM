using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Shelly_UI.ViewModels.Flatpak;

namespace Shelly_UI.Views.Flatpak;

public partial class FlatpakInstallWindow : ReactiveUserControl<FlatpakInstallViewModel>
{
    public FlatpakInstallWindow()
    {

        AvaloniaXamlLoader.Load(this);
        this.WhenActivated(disposables => { });
    }
}