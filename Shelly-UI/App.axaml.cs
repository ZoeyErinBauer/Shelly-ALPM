using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using Shelly_UI.Services;
using Shelly_UI.Services.AppCache;
using Shelly_UI.ViewModels;
using Shelly_UI.Views;

namespace Shelly_UI;

public partial class App : Application
{
    private ServiceProvider _services = null!;

    public static ServiceProvider Services => ((App)Current!)._services;
    
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
        collection.AddSingleton<ICredentialManager, CredentialManager>();
        collection.AddSingleton<IPrivilegedOperationService, PrivilegedOperationService>();
        collection.AddSingleton<IUnprivilegedOperationService, UnprivilegedOperationService>();
        collection.AddSingleton<ThemeService>();

        // Creates a ServiceProvider containing services from the provided IServiceCollection
        _services = collection.BuildServiceProvider();
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var configService = _services.GetRequiredService<IConfigService>();
            var themeService = _services.GetRequiredService<ThemeService>();
            var cacheService = _services.GetRequiredService<IAppCache>();
            var config = configService.LoadConfig();
            if (config.AccentColor != null) themeService.ApplyCustomAccent(Color.Parse(config.AccentColor));
            themeService.SetTheme(config.DarkMode);
            Assets.Resources.Culture = config.Culture != null ? new CultureInfo(config.Culture) : new CultureInfo("default");
        
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(configService, cacheService, AlpmService.Instance, _services),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}


