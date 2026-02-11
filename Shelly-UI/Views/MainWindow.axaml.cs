using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Res = Shelly_UI.Assets.Resources;
using Shelly_UI.Services;
using Shelly_UI.ViewModels;

namespace Shelly_UI.Views;

public partial class MainWindow :  ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        this.WhenActivated(disposables => { });
        AvaloniaXamlLoader.Load(this);
        
        Opened += async (_, _) =>
        {
            RestoreWindow();
            await CheckForUpdateAsync();
        };
        Closing += (_, _) => SaveWindow();
    }
    
    private void RestoreWindow()
    {
        var config = App.Services.GetRequiredService<IConfigService>().LoadConfig();
        
        Width = config.WindowWidth;
        Height = config.WindowHeight;
        
        if (config.WindowWidth == 0 || config.WindowHeight == 0)
        {
            Width = 800;
            Height = 600;
        }
        
        WindowState = config.WindowState;
    }

    private async Task CheckForUpdateAsync()
    {
        try
        {
            // Small delay to ensure window is fully positioned before showing dialog
            await Task.Delay(100);
            
            var updateService = new GitHubUpdateService();
            var hasUpdate = await updateService.CheckForUpdateAsync();
            if (!hasUpdate) return;

            // Brief delay before showing the update prompt dialog
            await Task.Delay(100);

            var dialog = new QuestionDialog(Res.NewVersionAvailable);
            var result = await dialog.ShowDialog<bool>(this);
            if (!result) return;

            await updateService.DownloadAndInstallUpdateAsync();
            
            // Restart application
            var currentProcess = Environment.ProcessPath;
            if (currentProcess != null)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = currentProcess,
                    UseShellExecute = true
                });
            }
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Update check failed: {ex.Message}");
        }
    }

    private void SaveWindow()
    {
        var configService = App.Services.GetRequiredService<IConfigService>();

        var (width, height) = this.ClientSize;

        var state = WindowState;
        
        var config = configService.LoadConfig();
        config.WindowWidth = width;
        config.WindowHeight = height;
        config.WindowState = state;
        
        configService.SaveConfig(config);
    }
    
    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.IsSettingsOpen = false;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is MainWindowViewModel vm)
        {
            vm.IsSettingsOpen = false;
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }
}