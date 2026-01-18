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

        // Dispose old disconnected connection if any
        _connection?.Dispose();

        _connection = new Connection(Address.System!);
        await _connection.ConnectAsync();
    }

    public async Task InitializeWithSyncAsync()
    {
        await CallMethodAsync("InitializeWithSync");
    }

    public async Task InitializeAsync()
    {
        await CallMethodAsync("Initialize");
    }

    public async Task SyncAsync(bool force = false)
    {
        await CallMethodAsync("Sync", writer => writer.WriteBool(force), "b");
    }

    public async Task<PackageInfo[]> GetInstalledPackagesAsync()
    {
        var jsonStrings = await CallMethodWithReplyAsync("GetInstalledPackages");
        return DeserializePackageInfoArray(jsonStrings);
    }

    public async Task<PackageInfo[]> GetAvailablePackagesAsync()
    {
        var jsonStrings = await CallMethodWithReplyAsync("GetAvailablePackages");
        return DeserializePackageInfoArray(jsonStrings);
    }

    public async Task<PackageUpdateInfo[]> GetPackagesNeedingUpdateAsync()
    {
        var jsonStrings = await CallMethodWithReplyAsync("GetPackagesNeedingUpdate");
        return DeserializePackageUpdateInfoArray(jsonStrings);
    }

    public async Task InstallPackagesAsync(string[] packageNames, uint flags = 0)
    {
        await CallMethodAsync("InstallPackages", writer =>
        {
            writer.WriteArray(packageNames);
            writer.WriteUInt32(flags);
        }, "asu");
    }

    public async Task RemovePackagesAsync(string[] packageNames, uint flags = 0)
    {
        await CallMethodAsync("RemovePackages", writer =>
        {
            writer.WriteArray(packageNames);
            writer.WriteUInt32(flags);
        }, "asu");
    }

    public async Task UpdatePackagesAsync(string[] packageNames, uint flags = 0)
    {
        await CallMethodAsync("UpdatePackages", writer =>
        {
            writer.WriteArray(packageNames);
            writer.WriteUInt32(flags);
        }, "asu");
    }

    public async Task SyncSystemUpdateAsync(uint flags = 0)
    {
        await CallMethodAsync("SyncSystemUpdate", writer => writer.WriteUInt32(flags), "u");
    }

    private async Task CallMethodAsync(string method, Action<MessageWriter>? writeArgs = null, string? signature = null)
    {
        for (int i = 0; i < 3; i++)
        {
            try
            {
                await EnsureConnectedAsync();
                var writer = _connection!.GetMessageWriter();
                writer.WriteMethodCallHeader(
                    destination: ShellyDbusConstants.ServiceName,
                    path: ShellyDbusConstants.ObjectPath,
                    @interface: ShellyDbusConstants.InterfaceName,
                    member: method,
                    signature: signature);

                writeArgs?.Invoke(writer);

                await _connection.CallMethodAsync(writer.CreateMessage());
                return;
            }
            catch (DisconnectedException) when (i < 2)
            {
                _connection?.Dispose();
                _connection = null;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[FUCKER] Failed to call method {method}: {ex}");
            }
        }
    }

    private async Task<string[]> CallMethodWithReplyAsync(string method)
    {
        for (int i = 0; i < 3; i++)
        {
            try
            {
                await EnsureConnectedAsync();
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
            catch (DisconnectedException) when (i < 2)
            {
                _connection?.Dispose();
                _connection = null;
            }
        }

        throw new Exception("D-Bus connection lost.");
    }

    private static PackageInfo[] DeserializePackageInfoArray(string[] jsonStrings)
    {
        var packages = new PackageInfo[jsonStrings.Length];
        for (int i = 0; i < jsonStrings.Length; i++)
        {
            packages[i] = JsonSerializer.Deserialize(jsonStrings[i], ShellyUIJsonContext.Default.PackageInfo) ??
                          new PackageInfo();
        }

        return packages;
    }

    private static PackageUpdateInfo[] DeserializePackageUpdateInfoArray(string[] jsonStrings)
    {
        var packages = new PackageUpdateInfo[jsonStrings.Length];
        for (int i = 0; i < jsonStrings.Length; i++)
        {
            packages[i] = JsonSerializer.Deserialize(jsonStrings[i], ShellyUIJsonContext.Default.PackageUpdateInfo) ??
                          new PackageUpdateInfo();
        }

        return packages;
    }

    private async Task EnsureConnectedAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ShellyServiceClient));

        if (!IsConnected)
        {
            await ConnectAsync();
        }
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