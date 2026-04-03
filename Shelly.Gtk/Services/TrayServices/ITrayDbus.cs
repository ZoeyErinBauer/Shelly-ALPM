namespace Shelly.Gtk.Services.TrayServices;

public interface ITrayDbus
{
    public Task RefreshSettingsAsync();

    public Task UpdatesMadeInUiAsync();
}