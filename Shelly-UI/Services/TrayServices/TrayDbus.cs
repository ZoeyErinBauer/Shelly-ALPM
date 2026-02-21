using System;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace Shelly_UI.Services.TrayServices;

public class TrayDBus : IDisposable
{
    private readonly Connection _connection = new(Address.Session!);
    
    public async Task RefreshSettingsAsync()
    {
        await _connection.ConnectAsync();
        await CallAsync("RefreshSettings");
    }

    public async Task CloseTrayAsync()
    {
        await _connection.ConnectAsync();
        await CallAsync("CloseTray");
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