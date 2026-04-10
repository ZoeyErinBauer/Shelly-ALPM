using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PackageManager.Alpm;
using PackageManager.Alpm.Events.EventArgs;
using PackageManager.Aur.Models;
using PackageManager.Utilities;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace PackageManager.Aur;

public class PackageProgressEventArgs : EventArgs
{
    public required string PackageName { get; init; }
    public int CurrentIndex { get; init; }
    public int TotalCount { get; init; }
    public PackageProgressStatus Status { get; init; }
    public string? Message { get; init; }
}

public class PkgbuildDiffRequestEventArgs : EventArgs
{
    public required string PackageName { get; init; }
    public required string OldPkgbuild { get; init; }
    public required string NewPkgbuild { get; init; }
    public bool ShowDiff { get; set; }
    public bool ProceedWithUpdate { get; set; } = true;
}

public class BuildOutputEventArgs : EventArgs
{
    public required string PackageName { get; init; }
    public required string Line { get; init; }
    public bool IsError { get; init; }
    public int? Percent { get; init; }
    public string? ProgressMessage { get; init; }
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
    : IAurPackageManager
{
    private AlpmManager _alpm;
    private AurSearchManager _aurSearchManager;
    private HttpClient _httpClient = new HttpClient();
    private List<string> _availablePackages = [];
    private readonly HashSet<string> _currentlyInstallingAurDeps = new();
    private bool _useChroot = false;
    private bool _noCheck = true;
    private string _chrootPath;

    public event EventHandler<PackageProgressEventArgs>? PackageProgress;
    public event EventHandler<PkgbuildDiffRequestEventArgs>? PkgbuildDiffRequest;
    public event EventHandler<AlpmQuestionEventArgs>? Question;
    public event EventHandler<AlpmProgressEventArgs>? Progress;
    public event EventHandler<BuildOutputEventArgs>? BuildOutput;
    public event EventHandler<AlpmPackageOperationEventArgs>? PackageOperation;
    public event EventHandler<AlpmScriptletEventArgs>? ScriptletInfo;
    public event EventHandler<AlpmHookEventArgs>? HookRun;
    public event EventHandler<AlpmReplacesEventArgs>? Replaces;
    public event EventHandler<AlpmPacnewEventArgs>? PacnewInfo;
    public event EventHandler<AlpmPacsaveEventArgs>? PacsaveInfo;
    public event EventHandler<AlpmErrorEventArgs>? ErrorEvent;

    public async Task Initialize(bool root = false, bool useTempPath = false, bool useChroot = false,
        string chrootPath = "/var/lib/shelly/chroot", string tempPath = "", bool showHiddenPackages = false,
        bool noCheck = true)
    {
        _alpm = configPath is null ? new AlpmManager() : new AlpmManager(configPath);
        _alpm.Initialize(root, useTempPath: useTempPath, tempPath: tempPath, showHiddenPackages: showHiddenPackages);
        _alpm.Question += (sender, args) => Question?.Invoke(this, args);
        _alpm.Progress += (sender, args) => Progress?.Invoke(this, args);
        _alpm.PackageOperation += (sender, args) => PackageOperation?.Invoke(this, args);
        _alpm.ScriptletInfo += (sender, args) => ScriptletInfo?.Invoke(this, args);
        _alpm.HookRun += (sender, args) => HookRun?.Invoke(this, args);
        _alpm.Replaces += (sender, args) => Replaces?.Invoke(this, args);
        _alpm.PacnewInfo += (sender, args) => PacnewInfo?.Invoke(this, args);
        _alpm.PacsaveInfo += (sender, args) => PacsaveInfo?.Invoke(this, args);
        _alpm.ErrorEvent += (sender, args) => ErrorEvent?.Invoke(this, args);
        _aurSearchManager = new AurSearchManager(_httpClient);
        _availablePackages = _alpm.GetAvailablePackages().Select(x => x.Name).ToList();
        _useChroot = useChroot;
        _chrootPath = chrootPath;
        _noCheck = noCheck;
        // Import caches from other AUR helpers (paru, yay) for installed foreign packages
        await ImportOtherAurHelperCaches();
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
        
        var allNames = searchResponse.Results.Select(x => x.Name)
            .Concat(suggestResponse)
            .Concat(suggestByBaseNameResponse)
            .Distinct()
            .ToList();

        if (allNames.Count == 0) return [];
        
        var fullInfoResponse = await _aurSearchManager.GetInfoAsync(allNames);

        return fullInfoResponse.Results;
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

    public async Task<string?> FetchPkgbuildAsync(string packageName)
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

    public async Task InstallDependenciesOnly(string packageName, bool includeMakeDeps = false)
    {
        PackageProgress?.Invoke(this, new PackageProgressEventArgs
        {
            PackageName = packageName,
            CurrentIndex = 1,
            TotalCount = 1,
            Status = PackageProgressStatus.Downloading,
            Message = "Downloading PKGBUILD to analyze dependencies"
        });

        var success = await DownloadPackage(packageName);

        if (!success)
        {
            PackageProgress?.Invoke(this, new PackageProgressEventArgs
            {
                PackageName = packageName,
                CurrentIndex = 1,
                TotalCount = 1,
                Status = PackageProgressStatus.Failed,
                Message = "Failed to download package"
            });
            return;
        }

        var user = Environment.GetEnvironmentVariable("SUDO_USER") ?? Environment.UserName;
        var home = $"/home/{user}";
        var tempPath = System.IO.Path.Combine(home, ".cache", "Shelly", packageName);
        var pkgbuildInfo = PkgbuildParser.Parse(System.IO.Path.Combine(tempPath, "PKGBUILD"));

        var depends = pkgbuildInfo.Depends.Select(x => x.Trim()).ToList();
        var depsToConsider = depends.ToList();

        if (includeMakeDeps)
        {
            var makeDepends = pkgbuildInfo.MakeDepends.Select(x => x.Trim()).ToList();
            depsToConsider = depsToConsider.Concat(makeDepends).Distinct().ToList();
        }

        var depsToInstall = depsToConsider.Where(x => !_alpm.IsDependencySatisfiedByInstalled(x)).ToList();

        if (depsToInstall.Count == 0)
        {
            PackageProgress?.Invoke(this, new PackageProgressEventArgs
            {
                PackageName = packageName,
                CurrentIndex = 1,
                TotalCount = 1,
                Status = PackageProgressStatus.Completed,
                Message = "All dependencies are already installed"
            });
            return;
        }

        PackageProgress?.Invoke(this, new PackageProgressEventArgs
        {
            PackageName = packageName,
            CurrentIndex = 1,
            TotalCount = 1,
            Status = PackageProgressStatus.Installing,
            Message = $"Installing dependencies: {string.Join(", ", depsToInstall)}"
        });


        var alpmPackages = new List<string>();
        var aurPackages = new List<string>();

        foreach (var dep in depsToInstall)
        {
            var repoName = _alpm.FindSatisfierInSyncDbs(dep);
            if (repoName != null)
                alpmPackages.Add(repoName);
            else
                aurPackages.Add(dep);
        }

        if (alpmPackages.Count > 0)
        {
            _alpm.InstallPackages(alpmPackages);
            _alpm.Refresh();
        }

        foreach (var pkg in aurPackages)
        {
            MakePkgAndInstallAurDependency(pkg);
        }


        PackageProgress?.Invoke(this, new PackageProgressEventArgs
        {
            PackageName = packageName,
            CurrentIndex = 1,
            TotalCount = 1,
            Status = PackageProgressStatus.Completed,
            Message = "Dependencies installed successfully"
        });
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
            var checkDepends = _noCheck ? new List<string>()
                : pkgbuildInfo.CheckDepends.Select(x => x.Trim()).ToList();
            var allDeps = pkgbuildInfo.Depends
                .Concat(pkgbuildInfo.MakeDepends)
                .Concat(checkDepends)
                .Select(x => x.Trim())
                .Distinct()
                .ToList();
            var depsToInstall = allDeps.Where(x => !_alpm.IsDependencySatisfiedByInstalled(x)).ToList();
            Console.Error.WriteLine($"dependency count {depsToInstall.Count}");
            var alpmPackages = new List<string>();
            var aurPackages = new List<string>();

            foreach (var dep in depsToInstall)
            {
                var repoName = _alpm.FindSatisfierInSyncDbs(dep);
                if (repoName != null)
                    alpmPackages.Add(repoName);
                else
                    aurPackages.Add(dep);
            }

            if (alpmPackages.Count > 0)
            {
                _alpm.InstallPackages(alpmPackages, AlpmTransFlag.AllDeps).Wait();
            }

            foreach (var pkg in aurPackages)
            {
                MakePkgAndInstallAurDependency(pkg);
            }


            // Backup PKGBUILD to PreviousVersions folder
            var previousVersionsPath = System.IO.Path.Combine(tempPath, "PreviousVersions");
            var pkgbuildPath = System.IO.Path.Combine(tempPath, "PKGBUILD");
            if (System.IO.File.Exists(pkgbuildPath))
            {
                // Create directory as the non-root user to avoid permission issues
                var mkdirProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "sudo",
                        Arguments = $"-u {user} mkdir -p {previousVersionsPath}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                mkdirProcess.Start();
                await mkdirProcess.WaitForExitAsync();

                var existingBackups = System.IO.Directory.Exists(previousVersionsPath)
                    ? System.IO.Directory.GetFiles(previousVersionsPath, "PKGBUILD.*")
                    : Array.Empty<string>();
                var nextNumber = existingBackups.Length + 1;
                var backupPath = System.IO.Path.Combine(previousVersionsPath, $"PKGBUILD.{nextNumber}");

                var cpProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "sudo",
                        Arguments = $"-u {user} cp {pkgbuildPath} {backupPath}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                cpProcess.Start();
                await cpProcess.WaitForExitAsync();
            }

            // Remove any existing package files before building
            foreach (var oldPkgFile in System.IO.Directory.GetFiles(tempPath, "*.pkg.tar.*"))
            {
                var rmPkgProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "sudo",
                        Arguments = $"-u {user} rm -f {oldPkgFile}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                rmPkgProcess.Start();
                await rmPkgProcess.WaitForExitAsync();
            }

            if (_useChroot)
            {
                EnsureChrootExists();
            }

            var buildProcess = CreateBuildProcess(tempPath);
            buildProcess.OutputDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                int? percent = null;
                string? progressMessage = null;
                if (e.Data.Contains('%'))
                {
                    var match = Regex.Match(e.Data, @"\[\s*(?<percent>\d+)%\]\s+(?<message>.+)");
                    if (match.Success)
                    {
                        percent = int.Parse(match.Groups["percent"].Value);
                        progressMessage = match.Groups["message"].Value;
                    }
                }
                BuildOutput?.Invoke(this, new BuildOutputEventArgs
                {
                    PackageName = packageName,
                    Line = e.Data,
                    IsError = false,
                    Percent = percent,
                    ProgressMessage = progressMessage
                });
            };

            buildProcess.ErrorDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                BuildOutput?.Invoke(this, new BuildOutputEventArgs
                {
                    PackageName = packageName,
                    Line = e.Data,
                    IsError = true
                });
            };

            buildProcess.Start();
            buildProcess.BeginOutputReadLine();
            buildProcess.BeginErrorReadLine();
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
                _alpm.Refresh();
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

    public async Task RemovePackages(List<string> packageNames, AlpmTransFlag flags = AlpmTransFlag.None)
    {
        _alpm.RemovePackages(packageNames, flags);
        foreach (var packageName in packageNames)
        {
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
        _httpClient?.Dispose();
        _aurSearchManager?.Dispose();
        _alpm?.Dispose();
    }

    public async Task InstallPackageVersion(string packageName, string commit)
    {
        PackageProgress?.Invoke(this, new PackageProgressEventArgs
        {
            PackageName = packageName,
            CurrentIndex = 1,
            TotalCount = 1,
            Status = PackageProgressStatus.Downloading
        });

        var success = await DownloadPackageAtCommit(packageName, commit);

        if (!success)
        {
            PackageProgress?.Invoke(this, new PackageProgressEventArgs
            {
                PackageName = packageName,
                CurrentIndex = 1,
                TotalCount = 1,
                Status = PackageProgressStatus.Failed,
                Message = "Failed to download package at specified commit"
            });
            throw new Exception($"Failed to download package {packageName} at commit {commit}");
        }

        var user = Environment.GetEnvironmentVariable("SUDO_USER") ?? Environment.UserName;
        var home = $"/home/{user}";
        var tempPath = System.IO.Path.Combine(home, ".cache", "Shelly", packageName);

        PackageProgress?.Invoke(this, new PackageProgressEventArgs
        {
            PackageName = packageName,
            CurrentIndex = 1,
            TotalCount = 1,
            Status = PackageProgressStatus.Building,
            Message = "Building package with makepkg"
        });

        var pkgbuildInfo = PkgbuildParser.Parse(System.IO.Path.Combine(tempPath, "PKGBUILD"));
        var checkDepends = _noCheck ? new List<string>()
            : pkgbuildInfo.CheckDepends.Select(x => x.Trim()).ToList();
        var allDeps = pkgbuildInfo.Depends
            .Concat(pkgbuildInfo.MakeDepends)
            .Concat(checkDepends)
            .Select(x => x.Trim())
            .Distinct()
            .ToList();
        var depsToInstall = allDeps.Where(x => !_alpm.IsDependencySatisfiedByInstalled(x)).ToList();
        var alpmPackages = new List<string>();
        var aurPackages = new List<string>();

        foreach (var dep in depsToInstall)
        {
            var repoName = _alpm.FindSatisfierInSyncDbs(dep);
            if (repoName != null)
                alpmPackages.Add(repoName);
            else
                aurPackages.Add(dep);
        }

        if (alpmPackages.Count > 0)
        {
            _alpm.InstallPackages(alpmPackages);
        }

        foreach (var pkg in aurPackages)
        {
            MakePkgAndInstallAurDependency(pkg);
        }


        if (_useChroot)
        {
            EnsureChrootExists();
        }

        var buildProcess = CreateBuildProcess(tempPath, "--noconfirm" + (_noCheck ? " --nocheck" : ""));
        buildProcess.OutputDataReceived += (sender, e) =>
        {
            if (string.IsNullOrEmpty(e.Data)) return;
            int? percent = null;
            string? progressMessage = null;
            if (e.Data.Contains('%'))
            {
                var match = Regex.Match(e.Data, @"\[\s*(?<percent>\d+)%\]\s+(?<message>.+)");
                if (match.Success)
                {
                    percent = int.Parse(match.Groups["percent"].Value);
                    progressMessage = match.Groups["message"].Value;
                }
            }
            BuildOutput?.Invoke(this, new BuildOutputEventArgs
            {
                PackageName = packageName,
                Line = e.Data,
                IsError = false,
                Percent = percent,
                ProgressMessage = progressMessage
            });
        };

        buildProcess.ErrorDataReceived += (sender, e) =>
        {
            if (string.IsNullOrEmpty(e.Data)) return;
            BuildOutput?.Invoke(this, new BuildOutputEventArgs
            {
                PackageName = packageName,
                Line = e.Data,
                IsError = true
            });
        };
        buildProcess.Start();
        buildProcess.BeginOutputReadLine();
        buildProcess.BeginErrorReadLine();
        await buildProcess.WaitForExitAsync();


        if (buildProcess.ExitCode != 0)
        {
            PackageProgress?.Invoke(this, new PackageProgressEventArgs
            {
                PackageName = packageName,
                CurrentIndex = 1,
                TotalCount = 1,
                Status = PackageProgressStatus.Failed,
                Message = "Failed to build package with makepkg"
            });
            throw new Exception($"Failed to build package {packageName}");
        }

        var pkgFiles = System.IO.Directory.GetFiles(tempPath, "*.pkg.tar.*");
        if (pkgFiles.Length == 0)
        {
            PackageProgress?.Invoke(this, new PackageProgressEventArgs
            {
                PackageName = packageName,
                CurrentIndex = 1,
                TotalCount = 1,
                Status = PackageProgressStatus.Failed,
                Message = "No package file found after build"
            });
            throw new Exception($"No package file found after building {packageName}");
        }

        PackageProgress?.Invoke(this, new PackageProgressEventArgs
        {
            PackageName = packageName,
            CurrentIndex = 1,
            TotalCount = 1,
            Status = PackageProgressStatus.Installing
        });

        _alpm.InstallLocalPackage(pkgFiles[0]);
        _alpm.Refresh();

        PackageProgress?.Invoke(this, new PackageProgressEventArgs
        {
            PackageName = packageName,
            CurrentIndex = 1,
            TotalCount = 1,
            Status = PackageProgressStatus.Completed
        });
    }

    private async Task<bool> DownloadPackageAtCommit(string packageName, string commit)
    {
        try
        {
            var user = Environment.GetEnvironmentVariable("SUDO_USER") ?? Environment.UserName;
            var home = $"/home/{user}";
            var tempPath = System.IO.Path.Combine(home, ".cache", "Shelly", packageName);

            // Remove existing directory if it exists
            if (System.IO.Directory.Exists(tempPath))
            {
                var rmProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "rm",
                        Arguments = $"-rf {tempPath}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                rmProcess.Start();
                await rmProcess.WaitForExitAsync();
            }

            // Clone the AUR git repository
            var cloneProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "sudo",
                    Arguments = $"-u {user} git clone https://aur.archlinux.org/{packageName}.git {tempPath}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            cloneProcess.Start();
            await cloneProcess.WaitForExitAsync();

            if (cloneProcess.ExitCode != 0)
            {
                return false;
            }

            // Checkout the specific commit
            var checkoutProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "sudo",
                    Arguments = $"-u {user} git checkout {commit}",
                    WorkingDirectory = tempPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            checkoutProcess.Start();
            await checkoutProcess.WaitForExitAsync();

            if (checkoutProcess.ExitCode != 0)
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

    private async Task<bool> DownloadPackage(string packageName)
    {
        try
        {
            var user = Environment.GetEnvironmentVariable("SUDO_USER") ?? Environment.UserName;
            var home = $"/home/{user}";
            var tempPath = System.IO.Path.Combine(home, ".cache", "Shelly", packageName);

            if (System.IO.Directory.Exists(System.IO.Path.Combine(tempPath, ".git")))
            {
                // Already cloned — pull latest
                var pullProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "sudo",
                        Arguments = $"-u {user} git pull",
                        WorkingDirectory = tempPath,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                pullProcess.Start();
                await pullProcess.WaitForExitAsync();
                if (pullProcess.ExitCode != 0) return false;
            }
            else
            {
                // Remove directory if it exists but isn't a git repo
                if (System.IO.Directory.Exists(tempPath))
                {
                    var rmProcess = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "rm",
                            Arguments = $"-rf {tempPath}",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    rmProcess.Start();
                    await rmProcess.WaitForExitAsync();
                }

                // Clone the AUR git repository
                var cloneProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "sudo",
                        Arguments = $"-u {user} git clone https://aur.archlinux.org/{packageName}.git {tempPath}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                cloneProcess.Start();
                await cloneProcess.WaitForExitAsync();
                if (cloneProcess.ExitCode != 0) return false;
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

    /// <summary>
    /// Imports cached AUR package data from other AUR helpers (paru and yay) into Shelly's cache.
    /// This allows Shelly to show PKGBUILD diffs for packages that were originally installed via paru or yay.
    /// </summary>
    private async Task ImportOtherAurHelperCaches()
    {
        try
        {
            var user = Environment.GetEnvironmentVariable("SUDO_USER") ?? Environment.UserName;
            var home = $"/home/{user}";
            var shellyCachePath = System.IO.Path.Combine(home, ".cache", "Shelly");

            // Get list of installed foreign (AUR) packages
            var foreignPackages = _alpm.GetForeignPackages().Select(p => p.Name).ToHashSet();

            // Define cache locations for other AUR helpers
            var paruCachePath = System.IO.Path.Combine(home, ".cache", "paru", "clone");
            var yayCachePath = System.IO.Path.Combine(home, ".cache", "yay");

            // Import from paru cache
            if (System.IO.Directory.Exists(paruCachePath))
            {
                await ImportFromAurHelperCache(paruCachePath, shellyCachePath, foreignPackages, user);
            }

            // Import from yay cache
            if (System.IO.Directory.Exists(yayCachePath))
            {
                await ImportFromAurHelperCache(yayCachePath, shellyCachePath, foreignPackages, user);
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail initialization if cache import fails
            Console.Error.WriteLine($"Warning: Failed to import AUR helper caches: {ex.Message}");
        }
    }

    /// <summary>
    /// Imports package caches from a specific AUR helper's cache directory.
    /// </summary>
    private async Task ImportFromAurHelperCache(string sourceCachePath, string shellyCachePath,
        HashSet<string> foreignPackages, string user)
    {
        try
        {
            var packageDirs = System.IO.Directory.GetDirectories(sourceCachePath);

            foreach (var packageDir in packageDirs)
            {
                var packageName = System.IO.Path.GetFileName(packageDir);

                // Only import if the package is currently installed as a foreign package
                if (!foreignPackages.Contains(packageName))
                    continue;

                var shellyPackagePath = System.IO.Path.Combine(shellyCachePath, packageName);

                // Skip if Shelly already has a cache for this package
                if (System.IO.Directory.Exists(shellyPackagePath))
                    continue;

                // Check if source has a PKGBUILD
                var sourcePkgbuild = System.IO.Path.Combine(packageDir, "PKGBUILD");
                if (!System.IO.File.Exists(sourcePkgbuild))
                    continue;

                // Create Shelly cache directory for this package
                var mkdirProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "sudo",
                        Arguments = $"-u {user} mkdir -p {shellyPackagePath}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                mkdirProcess.Start();
                await mkdirProcess.WaitForExitAsync();

                // Copy the PKGBUILD and other relevant files
                var copyProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "sudo",
                        Arguments = $"-u {user} cp -r {packageDir}/. {shellyPackagePath}/",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                copyProcess.Start();
                await copyProcess.WaitForExitAsync();

                // Remove any .git directory to save space (we don't need git history)
                var gitDir = System.IO.Path.Combine(shellyPackagePath, ".git");
                if (System.IO.Directory.Exists(gitDir))
                {
                    var rmGitProcess = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "sudo",
                            Arguments = $"-u {user} rm -rf {gitDir}",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    rmGitProcess.Start();
                    await rmGitProcess.WaitForExitAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Failed to import from {sourceCachePath}: {ex.Message}");
        }
    }

    private void MakePkgAndInstallAurDependency(string packageName)
    {
        if (!_currentlyInstallingAurDeps.Add(packageName))
        {
            Console.Error.WriteLine($"[Shelly] Skipping {packageName} - circular dependency detected");
            return;
        }

        try
        {
            var success = DownloadPackage(packageName).Result;
            if (!success)
            {
                PackageProgress?.Invoke(this, new PackageProgressEventArgs
                {
                    PackageName = packageName,
                    CurrentIndex = 1,
                    TotalCount = 1,
                    Status = PackageProgressStatus.Failed,
                    Message = "Failed to download package"
                });
                return;
            }

            var user = Environment.GetEnvironmentVariable("SUDO_USER") ?? Environment.UserName;
            var home = $"/home/{user}";
            var tempPath = System.IO.Path.Combine(home, ".cache", "Shelly", packageName);
            PackageProgress?.Invoke(this, new PackageProgressEventArgs
            {
                PackageName = packageName,
                CurrentIndex = 1,
                TotalCount = 1,
                Status = PackageProgressStatus.Building,
                Message = "Building package with makepkg"
            });
            var pkgbuildInfo = PkgbuildParser.Parse(System.IO.Path.Combine(tempPath, "PKGBUILD"));
            var checkDepends = _noCheck ? new List<string>()
                : pkgbuildInfo.CheckDepends.Select(x => x.Trim()).ToList();
            var allDeps = pkgbuildInfo.Depends
                .Concat(pkgbuildInfo.MakeDepends)
                .Concat(checkDepends)
                .Select(x => x.Trim())
                .Distinct()
                .ToList();
            var depsToInstall = allDeps.Where(x => !_alpm.IsDependencySatisfiedByInstalled(x)).ToList();
            var alpmPackages = new List<string>();
            var aurPackages = new List<string>();

            foreach (var dep in depsToInstall)
            {
                var repoName = _alpm.FindSatisfierInSyncDbs(dep);
                if (repoName != null)
                    alpmPackages.Add(repoName);
                else
                    aurPackages.Add(dep);
            }

            if (alpmPackages.Count > 0)
            {
                _alpm.InstallPackages(alpmPackages, AlpmTransFlag.AllDeps);
            }

            foreach (var pkg in aurPackages)
            {
                MakePkgAndInstallAurDependency(pkg);
            }

            if (_useChroot)
            {
                EnsureChrootExists();
            }

            var buildProcess = CreateBuildProcess(tempPath);
            buildProcess.OutputDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                int? percent = null;
                string? progressMessage = null;
                if (e.Data.Contains('%'))
                {
                    var match = Regex.Match(e.Data, @"\[\s*(?<percent>\d+)%\]\s+(?<message>.+)");
                    if (match.Success)
                    {
                        percent = int.Parse(match.Groups["percent"].Value);
                        progressMessage = match.Groups["message"].Value;
                    }
                }
                BuildOutput?.Invoke(this, new BuildOutputEventArgs
                {
                    PackageName = packageName,
                    Line = e.Data,
                    IsError = false,
                    Percent = percent,
                    ProgressMessage = progressMessage
                });
            };

            buildProcess.ErrorDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                BuildOutput?.Invoke(this, new BuildOutputEventArgs
                {
                    PackageName = packageName,
                    Line = e.Data,
                    IsError = true
                });
            };

            buildProcess.Start();
            buildProcess.BeginOutputReadLine();
            buildProcess.BeginErrorReadLine();
            buildProcess.WaitForExit();
            if (buildProcess.ExitCode != 0)
            {
                Console.Error.WriteLine(
                    $"[Shelly] Failed to build AUR dependency: {packageName} (exit code {buildProcess.ExitCode})");
                return;
            }

            var pkgFiles = System.IO.Directory.GetFiles(tempPath, "*.pkg.tar.*");
            if (pkgFiles.Length == 0)
            {
                Console.Error.WriteLine($"[Shelly] No package file found after building: {packageName}");
                return;
            }

            _alpm.InstallLocalPackage(pkgFiles[0], AlpmTransFlag.AllDeps);
            _alpm.Refresh();
            _availablePackages = _alpm.GetAvailablePackages().Select(x => x.Name).ToList();
        }
        finally

        {
            _currentlyInstallingAurDeps.Remove(packageName);
        }
    }

    private void EnsureChrootExists()
    {
        var chrootRoot = Path.Combine(_chrootPath, "root");
        if (Directory.Exists(chrootRoot))
        {
            UpdateChroot();
            CopyMakepkgConfToChroot();
            return;
        }

        Directory.CreateDirectory(_chrootPath);

        var initProcess = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "mkarchroot",
                Arguments = $"{chrootRoot} base-devel",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        initProcess.Start();
        initProcess.WaitForExit();

        if (initProcess.ExitCode != 0)
            throw new Exception("Failed to initialize chroot environment");

        CopyMakepkgConfToChroot();
    }

    private void UpdateChroot()
    {
        var chrootRoot = Path.Combine(_chrootPath, "root");
        var updateProcess = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "arch-nspawn",
                Arguments = $"{chrootRoot} shelly upgrade -n",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        updateProcess.Start();
        updateProcess.WaitForExit();
    }

    private void CopyMakepkgConfToChroot()
    {
        var destination = Path.Combine(_chrootPath, "root", "etc", "makepkg.conf");
        File.Copy("/etc/makepkg.conf", destination, overwrite: true);
    }

    private System.Diagnostics.Process CreateBuildProcess(string tempPath,
        string? makepkgArgs = null)
    {
        makepkgArgs ??= "-f -c --noconfirm --skippgpcheck" + (_noCheck ? " --nocheck" : "");
        if (_useChroot)
        {
            return new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "makechrootpkg",
                    Arguments = $"-c -r {_chrootPath}",
                    WorkingDirectory = tempPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
        }

        var user = Environment.GetEnvironmentVariable("SUDO_USER") ?? Environment.UserName;
        return new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "sudo",
                Arguments = $"-u {user} makepkg {makepkgArgs}",
                WorkingDirectory = tempPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
    }
}