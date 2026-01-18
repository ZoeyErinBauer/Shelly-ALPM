using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Shelly.Protocol;
using Tmds.DBus.Protocol;

namespace Shelly_UI.Services;

/// <summary>
/// D-Bus client for communicating with the Shelly package manager service.
/// This client runs in the unprivileged GUI and communicates with the privileged service over D-Bus.
/// </summary>
public class ShellyServiceClient : IDisposable
{
    private Connection? _connection;
    private bool _disposed;

    public bool IsConnected => _connection != null;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ShellyServiceClient));

        if (IsConnected)
            return;

        _connection = new Connection(Address.System!);
        await _connection.ConnectAsync();
    }

    public Task InitializeWithSyncAsync()
    {
        EnsureConnected();
        return CallMethodAsync("InitializeWithSync");
    }

    public Task InitializeAsync()
    {
        EnsureConnected();
        return CallMethodAsync("Initialize");
    }

    public Task SyncAsync(bool force = false)
    {
        EnsureConnected();
        return CallMethodAsync("Sync", writer => writer.WriteBool(force), "b");
    }

    public async Task<PackageInfo[]> GetInstalledPackagesAsync()
    {
        EnsureConnected();
        var jsonStrings = await CallMethodWithReplyAsync("GetInstalledPackages");
        return DeserializePackageInfoArray(jsonStrings);
    }

    public async Task<PackageInfo[]> GetAvailablePackagesAsync()
    {
        EnsureConnected();
        var jsonStrings = await CallMethodWithReplyAsync("GetAvailablePackages");
        return DeserializePackageInfoArray(jsonStrings);
    }

    public async Task<PackageUpdateInfo[]> GetPackagesNeedingUpdateAsync()
    {
        EnsureConnected();
        var jsonStrings = await CallMethodWithReplyAsync("GetPackagesNeedingUpdate");
        return DeserializePackageUpdateInfoArray(jsonStrings);
    }

    public Task InstallPackagesAsync(string[] packageNames, uint flags = 0)
    {
        EnsureConnected();
        return CallMethodAsync("InstallPackages", writer =>
        {
            writer.WriteArray(packageNames);
            writer.WriteUInt32(flags);
        }, "asu");
    }

    public Task RemovePackagesAsync(string[] packageNames, uint flags = 0)
    {
        EnsureConnected();
        return CallMethodAsync("RemovePackages", writer =>
        {
            writer.WriteArray(packageNames);
            writer.WriteUInt32(flags);
        }, "asu");
    }

    public Task UpdatePackagesAsync(string[] packageNames, uint flags = 0)
    {
        EnsureConnected();
        return CallMethodAsync("UpdatePackages", writer =>
        {
            writer.WriteArray(packageNames);
            writer.WriteUInt32(flags);
        }, "asu");
    }

    public Task SyncSystemUpdateAsync(uint flags = 0)
    {
        EnsureConnected();
        return CallMethodAsync("SyncSystemUpdate", writer => writer.WriteUInt32(flags), "u");
    }

    private Task CallMethodAsync(string method, Action<MessageWriter>? writeArgs = null, string? signature = null)
    {
        var writer = _connection!.GetMessageWriter();
        writer.WriteMethodCallHeader(
            destination: ShellyDbusConstants.ServiceName,
            path: ShellyDbusConstants.ObjectPath,
            @interface: ShellyDbusConstants.InterfaceName,
            member: method,
            signature: signature);

        writeArgs?.Invoke(writer);

        return _connection.CallMethodAsync(writer.CreateMessage());
    }

    private async Task<string[]> CallMethodWithReplyAsync(string method)
    {
        var writer = _connection!.GetMessageWriter();
        writer.WriteMethodCallHeader(
            destination: ShellyDbusConstants.ServiceName,
            path: ShellyDbusConstants.ObjectPath,
            @interface: ShellyDbusConstants.InterfaceName,
            member: method);

        var reply = await _connection.CallMethodAsync(writer.CreateMessage(), 
            static (Message message, object? state) =>
            {
                var reader = message.GetBodyReader();
                return reader.ReadArrayOfString();
            }, 
            null);
        
        return reply;
    }

    private static PackageInfo[] DeserializePackageInfoArray(string[] jsonStrings)
    {
        var packages = new PackageInfo[jsonStrings.Length];
        for (int i = 0; i < jsonStrings.Length; i++)
        {
            packages[i] = JsonSerializer.Deserialize<PackageInfo>(jsonStrings[i]) ?? new PackageInfo();
        }
        return packages;
    }

    private static PackageUpdateInfo[] DeserializePackageUpdateInfoArray(string[] jsonStrings)
    {
        var packages = new PackageUpdateInfo[jsonStrings.Length];
        for (int i = 0; i < jsonStrings.Length; i++)
        {
            packages[i] = JsonSerializer.Deserialize<PackageUpdateInfo>(jsonStrings[i]) ?? new PackageUpdateInfo();
        }
        return packages;
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to the Shelly service. Call ConnectAsync first.");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _connection?.Dispose();
        _connection = null;
        _disposed = true;
    }
}
