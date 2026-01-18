using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PackageManager.Alpm;

namespace Shelly_UI.Services;

/// <summary>
/// Implementation of IPackageService that communicates with the privileged Shelly.Service via D-Bus.
/// </summary>
public class PackageService : IPackageService, IDisposable
{
    private readonly ShellyServiceClient _client;
    private bool _disposed;

    public PackageService()
    {
        _client = new ShellyServiceClient();
    }

    public async Task ConnectAsync()
    {
        await _client.ConnectAsync();
    }

    public async Task InitializeWithSyncAsync()
    {
        await EnsureConnectedAsync();
        await _client.InitializeWithSyncAsync();
    }

    public async Task InitializeAsync()
    {
        await EnsureConnectedAsync();
        await _client.InitializeAsync();
    }

    public async Task SyncAsync(bool force = false)
    {
        await EnsureConnectedAsync();
        await _client.SyncAsync(force);
    }

    public async Task<List<AlpmPackageDto>> GetInstalledPackagesAsync()
    {
        await EnsureConnectedAsync();
        var packages = await _client.GetInstalledPackagesAsync();
        return packages.Select(p => new AlpmPackageDto
        {
            Name = p.Name,
            Version = p.Version,
            Size = p.Size,
            Description = p.Description,
            Url = p.Url,
            Repository = p.Repository
        }).ToList();
    }

    public async Task<List<AlpmPackageDto>> GetAvailablePackagesAsync()
    {
        await EnsureConnectedAsync();
        var packages = await _client.GetAvailablePackagesAsync();
        return packages.Select(p => new AlpmPackageDto
        {
            Name = p.Name,
            Version = p.Version,
            Size = p.Size,
            Description = p.Description,
            Url = p.Url,
            Repository = p.Repository
        }).ToList();
    }

    public async Task<List<AlpmPackageUpdateDto>> GetPackagesNeedingUpdateAsync()
    {
        await EnsureConnectedAsync();
        var packages = await _client.GetPackagesNeedingUpdateAsync();
        return packages.Select(p => new AlpmPackageUpdateDto
        {
            Name = p.Name,
            CurrentVersion = p.CurrentVersion,
            NewVersion = p.NewVersion,
            DownloadSize = p.DownloadSize
        }).ToList();
    }

    public async Task InstallPackagesAsync(List<string> packageNames, AlpmTransFlag flags = AlpmTransFlag.None)
    {
        await EnsureConnectedAsync();
        await _client.InstallPackagesAsync(packageNames.ToArray(), (uint)flags);
    }

    public async Task RemovePackagesAsync(List<string> packageNames, AlpmTransFlag flags = AlpmTransFlag.Cascade | AlpmTransFlag.Recurse | AlpmTransFlag.NoHooks | AlpmTransFlag.NoScriptlet )
    {
        try
        {
            await EnsureConnectedAsync();
            var uintFlags = (uint)flags;
            await _client.RemovePackagesAsync(packageNames.ToArray(), uintFlags);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG_LOG] Error removing packages: {ex.Message}");
            throw;
        }
    }

    public async Task UpdatePackagesAsync(List<string> packageNames, AlpmTransFlag flags = AlpmTransFlag.None)
    {
        await EnsureConnectedAsync();
        await _client.UpdatePackagesAsync(packageNames.ToArray(), (uint)flags);
    }

    public async Task SyncSystemUpdateAsync(AlpmTransFlag flags = AlpmTransFlag.None)
    {
        await EnsureConnectedAsync();
        await _client.SyncSystemUpdateAsync((uint)flags);
    }

    private async Task EnsureConnectedAsync()
    {
        if (!_client.IsConnected)
        {
            await _client.ConnectAsync();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _client.Dispose();
        _disposed = true;
    }
}
