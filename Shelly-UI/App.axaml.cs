using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Shelly_UI.Services;
using Shelly_UI.ViewModels;
using Shelly_UI.Views;

namespace Shelly_UI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
     
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var config = new ConfigService().LoadConfig();
            if (config.AccentColor != null) new ThemeService().ApplyCustomAccent(config.AccentColor);
            Assets.Resources.Culture = config.Culture != null ? new CultureInfo(config.Culture) : new CultureInfo("default");
        
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}