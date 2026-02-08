using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI.Avalonia;
using Shelly_UI.ViewModels;

namespace Shelly_UI.Views;

public partial class MetaSearchWindow : ReactiveUserControl<MetaSearchViewModel>
{
    public MetaSearchWindow()
    {
        InitializeComponent();
    }
}