using Tmds.DBus.Protocol;

namespace Shelly.Gtk.Services.TrayServices;

public class TrayDBus : ITrayDbus
{
    private readonly DBusConnection _connection = new(DBusAddress.Session!);
    
    public async Task RefreshSettingsAsync()
    {
        await _connection.ConnectAsync();
        await CallAsync("RefreshSettings");
    }

    public async Task UpdatesMadeInUiAsync()
    {
        await _connection.ConnectAsync();
        await CallAsync("UpdatesMadeInUi");
    }

    private Task CallAsync(string method)
    {
        var writer = _connection.GetMessageWriter();

        writer.WriteMethodCallHeader(
            destination: ShellyConstants.Service,
            path: ShellyConstants.Path,
            @interface: ShellyConstants.Interface,
            member: method,
            signature: null);

        return _connection.CallMethodAsync(writer.CreateMessage());
    }

    public void Dispose() => _connection.Dispose();
}