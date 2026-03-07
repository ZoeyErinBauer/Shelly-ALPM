using Gtk;
using Shelly.Gtk.Helpers;
using Shelly.Gtk.Services;
using Shelly.Gtk.UiModels;

namespace Shelly.Gtk.Windows;

public class Settings(
    IConfigService configService,
    IPrivilegedOperationService privilegedOperationService,
    IUnprivilegedOperationService unprivilegedOperationService,
    ILockoutService lockoutService) : IShellyWindow
{
    private Box _box = null!;
    private ShellyConfig _config = null!;

    public Widget CreateWindow()
    {
        var builder = Builder.NewFromString(ResourceHelper.LoadUiFile("UiFiles/SettingWindow.ui"), -1);
        _box = (Box)builder.GetObject("SettingWindow")!;

        _config = configService.LoadConfig();
        
        SetupSwitch("aur_switch", _config.AurEnabled, (v) => _config.AurEnabled = v, builder);
        SetupSwitch("flatpak_switch", _config.FlatPackEnabled, (v) => _config.FlatPackEnabled = v, builder);
        SetupSwitch("tray_switch", _config.TrayEnabled, (v) => _config.TrayEnabled = v, builder);
        SetupSwitch("no_confirm_switch", _config.NoConfirm, (v) => _config.NoConfirm = v, builder);

        var traySpin = (SpinButton)builder.GetObject("tray_interval_spin")!;
        traySpin.Value = _config.TrayCheckIntervalHours;
        traySpin.OnValueChanged += (s, e) =>
        {
            _config.TrayCheckIntervalHours = (int)traySpin.Value;
            SaveConfig();
        };

        var syncButton = (Button)builder.GetObject("sync_button")!;
        syncButton.OnClicked += (s, e) => { _ = ForceSyncAsync(); };

        var checkUpdatesButton = (Button)builder.GetObject("check_updates_button")!;
        checkUpdatesButton.OnClicked += (s, e) => { _ = CheckUpdatesAsync(); };

        /*var githubButton = (Button)builder.GetObject("github_button")!;
        githubButton.OnClicked += (s, e) => { OpenUrl("https://github.com/shelly-alpm/Shelly-ALPM"); };

        var coffeeButton = (Button)builder.GetObject("coffee_button")!;
        coffeeButton.OnClicked += (s, e) => { OpenUrl("https://www.buymeacoffee.com/shellyalpm"); };*/

        return _box;
    }

    private void SetupSwitch(string id, bool initialValue, Action<bool> updateAction, Builder builder)
    {
        var sw = (Switch)builder.GetObject(id)!;
        sw.Active = initialValue;
        sw.OnStateSet += (s, e) =>
        {
            updateAction(e.State);
            SaveConfig();
            return false;
        };
    }

    private void SaveConfig()
    {
        configService.SaveConfig(_config);
    }

    private async Task ForceSyncAsync()
    {
        try
        {
            lockoutService.Show("Synchronizing databases...");
            await privilegedOperationService.SyncDatabasesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error syncing databases: {ex.Message}");
        }
        finally
        {
            lockoutService.Hide();
        }
    }

    private async Task CheckUpdatesAsync()
    {
     
    }

    private void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error opening URL: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _box.Dispose();
    }
}