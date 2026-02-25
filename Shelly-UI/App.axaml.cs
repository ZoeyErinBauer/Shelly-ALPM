using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using Shelly_UI.Services;
using Shelly_UI.Services.AppCache;
using Shelly_UI.Services.LocalDatabase;
using Shelly_UI.Services.TrayServices;
using Shelly_UI.ViewModels;
using Shelly_UI.Views;

namespace Shelly_UI;

public partial class App : Application
{
    private ServiceProvider _services = null!;

    public static ServiceProvider Services => ((App)Current!)._services;

    private Window? _mainWindow;
    
    public App()
    {
        // Check for detecting system culture
        // Still in testing
        var systemCulture = CultureInfo.InstalledUICulture;
        //var systemCulture = new CultureInfo("pt-BR");

        CultureInfo.CurrentCulture = systemCulture;
        CultureInfo.CurrentUICulture = systemCulture;
        CultureInfo.DefaultThreadCurrentCulture = systemCulture;
        CultureInfo.DefaultThreadCurrentUICulture = systemCulture;
        Console.WriteLine($"CurrentCulture: {CultureInfo.CurrentCulture}");
        Console.WriteLine($"CurrentUICulture: {CultureInfo.CurrentUICulture}");
        Console.WriteLine($"InstalledUICulture: {CultureInfo.InstalledUICulture}");
        Console.WriteLine($"DefaultThreadCurrentCulture: {CultureInfo.DefaultThreadCurrentCulture}");
        Console.WriteLine($"DefaultThreadCurrentUICulture: {CultureInfo.DefaultThreadCurrentUICulture}");
        Console.WriteLine($"ArchNews resource: {Assets.Resources.ArchNews}");
        Console.WriteLine($"NoUpdateAvailable resource: {Assets.Resources.NoUpdateAvailable}");
        DataContext = this;
    }

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
        collection.AddSingleton<IAlpmEventService, AlpmEventService>();
        collection.AddSingleton<IPrivilegedOperationService, PrivilegedOperationService>();
        collection.AddSingleton<IUnprivilegedOperationService, UnprivilegedOperationService>();
        collection.AddSingleton<IDatabaseService, DatabaseService>();
        collection.AddSingleton<ThemeService>();

        // Creates a ServiceProvider containing services from the provided IServiceCollection
        _services = collection.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var configService = _services.GetRequiredService<IConfigService>();
            var themeService = _services.GetRequiredService<ThemeService>();
            var cacheService = _services.GetRequiredService<IAppCache>();
            var config = configService.LoadConfig();
            var sessionDesktop = Environment.GetEnvironmentVariable("XDG_SESSION_DESKTOP");
            if (config.UseKdeTheme && sessionDesktop == "KDE")
            {
                themeService.ApplyKdeTheme();
            }
            else
            {
                if (config.AccentColor != null) themeService.ApplyCustomAccent(Color.Parse(config.AccentColor));
                ThemeService.SetTheme(config.DarkMode);
            }

            Assets.Resources.Culture =
                config.Culture != null ? new CultureInfo(config.Culture) : new CultureInfo("default");

            _mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(configService, cacheService, _services.GetRequiredService<IAlpmEventService>(), _services),
            };

            desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;

            if (config.TrayEnabled)
            {
                TrayStartService.Start();
            }

            desktop.MainWindow = _mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

}

internal class SimpleCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
}