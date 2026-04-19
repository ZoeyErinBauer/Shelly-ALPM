using Gtk;
using Shelly.Gtk.Helpers;
using Shelly.Gtk.Services;
using Shelly.Gtk.UiModels;

namespace Shelly.Gtk.Windows;

public class SetupWindow(
    IConfigService configService,
    IPrivilegedOperationService privilegedOperationService,
    ILockoutService lockoutService,
    IGenericQuestionService genericQuestionService) : IShellyWindow
{
    private Box _box = null!;
    public event EventHandler? SetupFinished;

    public Widget CreateWindow()
    {
        var builder = Builder.NewFromString(ResourceHelper.LoadUiFile("UiFiles/SetupWindow.ui"), -1);
        _box = (Box)builder.GetObject("SetupWindow")!;

        var aurCheck = (CheckButton)builder.GetObject("aur_check")!;
        var flatpakCheck = (CheckButton)builder.GetObject("flatpak_check")!;
        var appimageCheck = (CheckButton)builder.GetObject("appimage_check")!;
        var trayCheck = (CheckButton)builder.GetObject("tray_check")!;
        var finishButton = (Button)builder.GetObject("finish_button")!;
        
        var currentConfig = configService.LoadConfig();
        aurCheck.Active = currentConfig.AurEnabled;
        flatpakCheck.Active = currentConfig.FlatPackEnabled;
        appimageCheck.Active = currentConfig.AppImageEnabled;
        trayCheck.Active = currentConfig.TrayEnabled;
        
        finishButton.OnClicked += (_, _) =>
        {
            var config = configService.LoadConfig();
            config.AurEnabled = aurCheck.Active;
            config.FlatPackEnabled = flatpakCheck.Active;
            config.AppImageEnabled = appimageCheck.Active;
            config.TrayEnabled = trayCheck.Active;
            config.NewInstallInitSettings = true;

            configService.SaveConfig(config);
            SetupFinished?.Invoke(this, EventArgs.Empty);

            if (!flatpakCheck.Active) return;
            try
            {
                var result = privilegedOperationService.IsPackageInstalledOnMachine("flatpak").Result;

                if (result) return;

                lockoutService.Show("Installing flatpak...");
                var instalResult = privilegedOperationService.InstallPackagesAsync(["flatpak"]).Result;

                if (instalResult.Success)
                {
                    genericQuestionService.RaiseToastMessage(
                        new ToastMessageEventArgs("Reboot required after flatpak installation."));
                }
                else
                {
                    Console.WriteLine($"Failed to install flatpak");
                    config.FlatPackEnabled = false;
                    configService.SaveConfig(config);
                }
            }
            catch (Exception ex)
            {
                genericQuestionService.RaiseToastMessage(
                    new ToastMessageEventArgs("Reboot required after flatpak installation."));
                Console.WriteLine($"Error installing flatpak: {ex.Message}");
                config.FlatPackEnabled = false;
                configService.SaveConfig(config);
            }
            finally
            {
                lockoutService.Hide();
            }
        };

        return _box;
    }

    public void Dispose()
    {
        _box.Unparent();
    }
}