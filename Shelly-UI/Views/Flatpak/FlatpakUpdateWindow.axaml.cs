using Avalonia.Markup.Xaml;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Shelly_UI.ViewModels.Flatpak;

namespace Shelly_UI.Views.Flatpak;

public partial class FlatpakUpdateWindow : ReactiveUserControl<FlatpakUpdateViewModel>
{
    public FlatpakUpdateWindow()
    {

        AvaloniaXamlLoader.Load(this);
        this.WhenActivated(disposables => { });
    }
}