using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using PackageManager.Alpm;
using PackageManager.Aur.Models;
using PackageManager.Utilities;

namespace PackageManager.Aur;

public class PackageProgressEventArgs : EventArgs
{
    public string PackageName { get; init; }
    public int CurrentIndex { get; init; }
    public int TotalCount { get; init; }
    public PackageProgressStatus Status { get; init; }
    public string? Message { get; init; }
}

public enum PackageProgressStatus
{
    Downloading,
    Building,
    Installing,
    Completed,
    Failed
}

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
    
    public event EventHandler<PackageProgressEventArgs>? PackageProgress;

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

    public async Task<List<AurPackageDto>> SearchPackages(string query)
    {
        var searchResponse = await _aurSearchManager.SearchAsync(query);
        var suggestResponse = await _aurSearchManager.SuggestAsync(query);
        var suggestByBaseNameResponse = await _aurSearchManager.SuggestByPackageBaseNamesAsync(query);
        //this might fail if AUR is down.
        return searchResponse.Results.Concat(suggestResponse.Results)
            .Concat(suggestByBaseNameResponse.Results).ToList();
    }

    public async Task<List<AurUpdateDto>> GetPackagesNeedingUpdate()
    {
        List<AurUpdateDto> packagesToUpdate = [];
        var packages = _alpm.GetForeignPackages();
        var response = await _aurSearchManager.GetInfoAsync(packages.Select(x => x.Name).ToList());
        foreach (var pkg in response.Results)
        {
            var installedPkg = packages.FirstOrDefault(x => x.Name == pkg.Name);
            if (installedPkg is null) continue;
            if (VersionComparer.IsNewer(pkg.Version, installedPkg.Version))
            {
                packagesToUpdate.Add(new AurUpdateDto
                {
                    Name = pkg.Name,
                    Version = installedPkg.Version,
                    NewVersion = pkg.Version,
                    Url = pkg.Url,
                    PackageBase = pkg.PackageBase,
                    Description = pkg.Description
                });
            }
        }
        throw new System.NotImplementedException();
    }

    public Task UpdatePackages(List<string> packageNames)
    {
        throw new System.NotImplementedException();
    }

    public async Task InstallPackages(List<string> packageNames)
    {
        var totalCount = packageNames.Count;
        for (var i = 0; i < packageNames.Count; i++)
        {
            var packageName = packageNames[i];
            
            PackageProgress?.Invoke(this, new PackageProgressEventArgs
            {
                PackageName = packageName,
                CurrentIndex = i + 1,
                TotalCount = totalCount,
                Status = PackageProgressStatus.Downloading
            });
            
            var success = await DownloadPackage(packageName);
            
            if (!success)
            {
                PackageProgress?.Invoke(this, new PackageProgressEventArgs
                {
                    PackageName = packageName,
                    CurrentIndex = i + 1,
                    TotalCount = totalCount,
                    Status = PackageProgressStatus.Failed,
                    Message = "Failed to download package"
                });
                continue;
            }
            
            // Build the package using makepkg
            var tempPath = $"/tmp/{packageName}";
            PackageProgress?.Invoke(this, new PackageProgressEventArgs
            {
                PackageName = packageName,
                CurrentIndex = i + 1,
                TotalCount = totalCount,
                Status = PackageProgressStatus.Building,
                Message = "Building package with makepkg"
            });
            
            var buildProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "makepkg",
                    Arguments = "-s --noconfirm",
                    WorkingDirectory = tempPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            buildProcess.Start();
            await buildProcess.WaitForExitAsync();
            
            if (buildProcess.ExitCode != 0)
            {
                PackageProgress?.Invoke(this, new PackageProgressEventArgs
                {
                    PackageName = packageName,
                    CurrentIndex = i + 1,
                    TotalCount = totalCount,
                    Status = PackageProgressStatus.Failed,
                    Message = "Failed to build package with makepkg"
                });
                continue;
            }
            
            // Find the built package file
            var pkgFiles = System.IO.Directory.GetFiles(tempPath, "*.pkg.tar.*");
            if (pkgFiles.Length == 0)
            {
                PackageProgress?.Invoke(this, new PackageProgressEventArgs
                {
                    PackageName = packageName,
                    CurrentIndex = i + 1,
                    TotalCount = totalCount,
                    Status = PackageProgressStatus.Failed,
                    Message = "No package file found after build"
                });
                continue;
            }
            
            // Install using _alpm
            PackageProgress?.Invoke(this, new PackageProgressEventArgs
            {
                PackageName = packageName,
                CurrentIndex = i + 1,
                TotalCount = totalCount,
                Status = PackageProgressStatus.Installing
            });
            
            try
            {
                _alpm.InstallLocalPackage(pkgFiles[0]);
            }
            catch (Exception ex)
            {
                PackageProgress?.Invoke(this, new PackageProgressEventArgs
                {
                    PackageName = packageName,
                    CurrentIndex = i + 1,
                    TotalCount = totalCount,
                    Status = PackageProgressStatus.Failed,
                    Message = $"Failed to install package: {ex.Message}"
                });
                continue;
            }
            
            PackageProgress?.Invoke(this, new PackageProgressEventArgs
            {
                PackageName = packageName,
                CurrentIndex = i + 1,
                TotalCount = totalCount,
                Status = PackageProgressStatus.Completed
            });
        }
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

    private async Task<bool> DownloadPackage(string packageName)
    {
        try
        {
            var url = $"https://aur.archlinux.org/cgit/aur.git/snapshot/{packageName}.tar.gz";
            var tempPath = $"/tmp/{packageName}";

            // Download the package tarball
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            // Create temp directory if it doesn't exist
            if (!System.IO.Directory.Exists(tempPath))
            {
                System.IO.Directory.CreateDirectory(tempPath);
            }

            // Save the tarball
            var tarballPath = System.IO.Path.Combine(tempPath, $"{packageName}.tar.gz");
            await using (var fileStream = System.IO.File.Create(tarballPath))
            {
                await response.Content.CopyToAsync(fileStream);
            }

            // Extract the tarball
            var extractProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "tar",
                    Arguments = $"-xzf {tarballPath} -C {tempPath} --strip-components=1",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            extractProcess.Start();
            await extractProcess.WaitForExitAsync();

            if (extractProcess.ExitCode != 0)
            {
                return false;
            }

            // Copy PKGBUILD to aurSyncPath
            var pkgbuildSource = System.IO.Path.Combine(tempPath, "PKGBUILD");
            if (!System.IO.File.Exists(pkgbuildSource))
            {
                return false;
            }

            if (!System.IO.Directory.Exists(aurSyncPath))
            {
                System.IO.Directory.CreateDirectory(aurSyncPath);
            }

            var pkgbuildDest = System.IO.Path.Combine(aurSyncPath, $"{packageName}.PKGBUILD");
            System.IO.File.Copy(pkgbuildSource, pkgbuildDest, overwrite: true);

            return true;
        }
        catch
        {
            return false;
        }
    }
}