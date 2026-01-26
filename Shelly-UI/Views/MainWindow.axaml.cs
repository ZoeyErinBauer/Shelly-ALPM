using Avalonia;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Shelly_UI.Services;
using Shelly_UI.ViewModels;

namespace Shelly_UI.Views;

public partial class MainWindow :  ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        this.WhenActivated(disposables => { });
        AvaloniaXamlLoader.Load(this);
        
        Opened += (_, _) => RestoreWindow();
        Closing += (_, _) => SaveWindow();
    }
    
    private void RestoreWindow()
    {
        var config = App.Services.GetRequiredService<IConfigService>().LoadConfig();
        
        Width = config.WindowWidth;
        Height = config.WindowHeight;
    }

    private void SaveWindow()
    {
        var configService = App.Services.GetRequiredService<IConfigService>();

        var size = this.ClientSize;
        var width = size.Width;
        var height = size.Height;
        
        var config = configService.LoadConfig();
        config.WindowWidth = width;
        config.WindowHeight = height;
        
        configService.SaveConfig(config);
    }
}