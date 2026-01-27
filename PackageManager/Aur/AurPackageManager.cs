using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
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

public class PkgbuildDiffRequestEventArgs : EventArgs
{
    public string PackageName { get; init; }
    public string OldPkgbuild { get; init; }
    public string NewPkgbuild { get; init; }
    public bool ShowDiff { get; set; }
    public bool ProceedWithUpdate { get; set; } = true;
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
public class AurPackageManager(string? configPath = null)
    : IAurPackageManager, IDisposable
{
    private AlpmManager _alpm;
    private AurSearchManager _aurSearchManager;
    private HttpClient _httpClient = new HttpClient();

    public event EventHandler<PackageProgressEventArgs>? PackageProgress;
    public event EventHandler<PkgbuildDiffRequestEventArgs>? PkgbuildDiffRequest;

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
                    Url = pkg.Url ?? string.Empty,
                    PackageBase = pkg.PackageBase,
                    Description = pkg.Description ?? string.Empty
                });
            }
        }

        return packagesToUpdate;
    }

    public async Task UpdatePackages(List<string> packageNames)
    {
        var packagesToUpdate = new List<string>();

        foreach (var packageName in packageNames)
        {
            // Check if there's an existing PKGBUILD (cached from previous install)
            var user = Environment.GetEnvironmentVariable("SUDO_USER") ?? Environment.UserName;
            var home = $"/home/{user}";
            var tempPath = System.IO.Path.Combine(home, ".cache", "Shelly", packageName);
            var cachedPkgbuildPath = System.IO.Path.Combine(tempPath, "PKGBUILD");
            string? oldPkgbuild = null;

            if (System.IO.File.Exists(cachedPkgbuildPath))
            {
                oldPkgbuild = await System.IO.File.ReadAllTextAsync(cachedPkgbuildPath);
            }

            // Fetch the new PKGBUILD from AUR
            var newPkgbuild = await FetchPkgbuildAsync(packageName);

            if (oldPkgbuild != null && newPkgbuild != null && PkgbuildDiffRequest != null)
            {
                var args = new PkgbuildDiffRequestEventArgs
                {
                    PackageName = packageName,
                    OldPkgbuild = oldPkgbuild,
                    NewPkgbuild = newPkgbuild,
                    ShowDiff = false,
                    ProceedWithUpdate = true
                };

                PkgbuildDiffRequest.Invoke(this, args);

                if (!args.ProceedWithUpdate)
                {
                    continue;
                }
            }

            packagesToUpdate.Add(packageName);
        }

        if (packagesToUpdate.Count > 0)
        {
            await InstallPackages(packagesToUpdate);
        }
    }

    private async Task<string?> FetchPkgbuildAsync(string packageName)
    {
        try
        {
            var url = $"https://aur.archlinux.org/cgit/aur.git/plain/PKGBUILD?h={packageName}";
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
        }
        catch
        {
            // Ignore errors fetching PKGBUILD
        }

        return null;
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
            var user = Environment.GetEnvironmentVariable("SUDO_USER") ?? Environment.UserName;
            var home = $"/home/{user}";
            var tempPath = System.IO.Path.Combine(home, ".cache", "Shelly", packageName);
            PackageProgress?.Invoke(this, new PackageProgressEventArgs
            {
                PackageName = packageName,
                CurrentIndex = i + 1,
                TotalCount = totalCount,
                Status = PackageProgressStatus.Building,
                Message = "Building package with makepkg"
            });
            var pkgbuildInfo = PkgbuildParser.Parse(System.IO.Path.Combine(tempPath, "PKGBUILD"));
            var depends = pkgbuildInfo.Depends.Select(x => x.Trim()).ToList();
            var makeDepends = pkgbuildInfo.MakeDepends.Select(x => x.Trim()).ToList();
            var allDeps = depends.Concat(makeDepends).Distinct().ToList();
            var installedPackages = _alpm.GetInstalledPackages().ToDictionary(x => x.Name, x => x.Version);
            var depsToInstall = allDeps.Where(x => !IsDependencySatisfied(x, installedPackages)).ToList();

            foreach (var dep in depsToInstall)
            {
                try
                {
                    _alpm.InstallPackages(depsToInstall);
                }
                catch (Exception ex)
                {
                    try
                    {
                        var pkgName = _alpm.GetPackageNameFromProvides(dep);
                        _alpm.InstallPackage(pkgName);
                    }
                    catch (Exception ex2)
                    {
                        Console.Error.WriteLine("Failed to install dependency: " + ex2.Message + "");
                    }
                }
            }


            var buildProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "sudo",
                    Arguments = $"-u {user} makepkg --noconfirm",
                    WorkingDirectory = tempPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
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

    public async Task RemovePackages(List<string> packageNames)
    {
        foreach (var packageName in packageNames)
        {
            // Remove package via ALPM
            _alpm.RemovePackage(packageName);

            // Clean up cache folder
            var user = Environment.GetEnvironmentVariable("SUDO_USER") ?? Environment.UserName;
            var home = $"/home/{user}";
            var cachePath = System.IO.Path.Combine(home, ".cache", "Shelly", packageName);

            if (System.IO.Directory.Exists(cachePath))
            {
                // Remove cache directory as the original user
                var rmProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "sudo",
                        Arguments = $"-u {user} rm -rf {cachePath}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                rmProcess.Start();
                await rmProcess.WaitForExitAsync();
            }
        }
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
            var user = Environment.GetEnvironmentVariable("SUDO_USER") ?? Environment.UserName;
            var home = $"/home/{user}";
            var tempPath = System.IO.Path.Combine(home, ".cache", "Shelly", packageName);

            // Download the package tarball
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            // Create temp directory if it doesn't exist (run as non-root user)
            if (!System.IO.Directory.Exists(tempPath))
            {
                var mkdirProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "sudo",
                        Arguments = $"-u {user} mkdir -p {tempPath}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                mkdirProcess.Start();
                await mkdirProcess.WaitForExitAsync();
            }

            // Save the tarball
            var tarballPath = System.IO.Path.Combine(tempPath, $"{packageName}.tar.gz");
            await using (var fileStream = System.IO.File.Create(tarballPath))
            {
                await response.Content.CopyToAsync(fileStream);
            }

            // Extract the tarball (run as non-root user)
            var extractProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "sudo",
                    Arguments = $"-u {user} tar -xzf {tarballPath} -C {tempPath} --strip-components=1",
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

            // Verify PKGBUILD exists
            var pkgbuildSource = System.IO.Path.Combine(tempPath, "PKGBUILD");
            return System.IO.File.Exists(pkgbuildSource);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDependencySatisfied(string dependency, Dictionary<string, string> installedPackages)
    {
        // Parse dependency: "package>=1.0", "package>2.0", "package=1.5", etc.
        var match = Regex.Match(dependency, @"^([a-zA-Z0-9@._+-]+)(>=|<=|>|<|=)?(.+)?$");
        if (!match.Success) return false;

        var pkgName = match.Groups[1].Value;
        var op = match.Groups[2].Value;
        var requiredVersion = match.Groups[3].Value;

        if (!installedPackages.TryGetValue(pkgName, out var installedVersion))
            return false; // Not installed

        if (string.IsNullOrEmpty(op))
            return true; // No version constraint, just needs to be installed

        var cmp = VersionComparer.Compare(installedVersion, requiredVersion);

        return op switch
        {
            ">=" => cmp >= 0,
            "<=" => cmp <= 0,
            ">" => cmp > 0,
            "<" => cmp < 0,
            "=" => cmp == 0,
            _ => true
        };
    }
}