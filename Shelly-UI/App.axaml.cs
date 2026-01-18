using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Shelly_UI.Services;
using Shelly_UI.Services.AppCache;
using Shelly_UI.ViewModels;
using Shelly_UI.Views;

namespace Shelly_UI;

public partial class App : Application
{
    private ServiceProvider _services = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Register all the services needed for the application to run
        var collection = new ServiceCollection();
        collection.AddSingleton<IConfigService, ConfigService>();
        collection.AddSingleton<IAppCache, AppCache>();
        collection.AddSingleton<IUpdateService, GitHubUpdateService>();
        collection.AddSingleton<ThemeService>();
        collection.AddSingleton<IPackageService, PackageService>();

        // Creates a ServiceProvider containing services from the provided IServiceCollection
        _services = collection.BuildServiceProvider();
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var configService = _services.GetRequiredService<IConfigService>();
            var themeService = _services.GetRequiredService<ThemeService>();
            var cacheService = _services.GetRequiredService<IAppCache>();
            var packageService = _services.GetRequiredService<IPackageService>();
            var config = configService.LoadConfig();
            if (config.AccentColor != null) themeService.ApplyCustomAccent(config.AccentColor);
            themeService.SetTheme(config.DarkMode);
            Assets.Resources.Culture = config.Culture != null ? new CultureInfo(config.Culture) : new CultureInfo("default");
        
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(configService, cacheService, packageService, _services),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}


