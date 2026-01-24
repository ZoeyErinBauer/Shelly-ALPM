using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using PackageManager.Alpm;
using PackageManager.Aur.Models;

namespace PackageManager.Aur;

/// <summary>
/// This is a manager for Arch universal repositories. It relies on <see cref="AlpmManager"/> to handle downloading and
/// installation of packages from the Arch User Repository (AUR).
/// </summary>
public class AurPackageManager(string? configPath = null, string? aurSyncPath = "/usr/bin/shelly/aur/")
    : IAurPackageManager, IDisposable
{
    private AlpmManager _alpm;
    private AurSearchManager _aurSearchManager;
    private HttpClient _httpClient = new HttpClient();

    public Task Initialize(bool root = false)
    {
        _alpm = configPath is null ? new AlpmManager() : new AlpmManager(configPath);
        _alpm.Initialize(root);
        _aurSearchManager = new AurSearchManager(_httpClient);
        return Task.CompletedTask;
    }

    public async Task<List<AurPackageDto>> GetInstalledPackages()
    {
        var foreignPackages = _alpm.GetForeignPackages();
        var response = await _aurSearchManager.GetInfoAsync(foreignPackages.Select(x => x.Name).ToList());
        return response.Results;
    }

    public Task<List<AurPackageDto>> SearchPackages(string query)
    {
        var response = _aurSearchManager.SearchAsync(query);
        throw new System.NotImplementedException();
    }

    public Task<List<AurPackageDto>> GetPackagesNeedingUpdate()
    {
        throw new System.NotImplementedException();
    }

    public Task UpdatePackages(List<string> packageNames)
    {
        throw new System.NotImplementedException();
    }

    public Task InstallPackages(List<string> packageNames)
    {
        throw new System.NotImplementedException();
    }

    public Task RemovePackages(List<string> packageNames)
    {
        throw new System.NotImplementedException();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _aurSearchManager.Dispose();
        _alpm.Dispose();
    }
}