using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace PackageManager.Alpm;

public class AlpmWorkerClient : IAlpmManager
{
    private readonly string _workerPath;
    private readonly Func<string?>? _passwordProvider;

    public AlpmWorkerClient(string workerPath, Func<string?>? passwordProvider = null)
    {
        _workerPath = workerPath;
        _passwordProvider = passwordProvider;
    }

    private string RunWorker(string command, string? args = null)
    {
        var password = _passwordProvider?.Invoke();

        var processInfo = new ProcessStartInfo
        {
            FileName = "sudo",
            Arguments = $"-S {_workerPath} {command} {args ?? ""}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo) ?? throw new Exception("Failed to start worker process.");
        
        if (!string.IsNullOrEmpty(password))
        {
            process.StandardInput.WriteLine(password);
            process.StandardInput.Flush();
        }
        else
        {
            throw new Exception("Authentication required: No password provided.");
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new Exception($"Worker failed (ExitCode: {process.ExitCode}): {error}");
        }

        return output;
    }

    public void IntializeWithSync() => RunWorker("Sync");

    public void Initialize() { /* Worker initializes per command */ }

    public void Sync(bool force = false) => RunWorker("Sync");

    public List<AlpmPackageDto> GetInstalledPackages()
    {
        var json = RunWorker("GetInstalledPackages");
        return JsonSerializer.Deserialize<List<AlpmPackageDto>>(json) ?? new List<AlpmPackageDto>();
    }

    public List<AlpmPackageDto> GetAvailablePackages()
    {
        var json = RunWorker("GetAvailablePackages");
        return JsonSerializer.Deserialize<List<AlpmPackageDto>>(json) ?? new List<AlpmPackageDto>();
    }

    public List<AlpmPackageUpdateDto> GetPackagesNeedingUpdate()
    {
        var json = RunWorker("GetPackagesNeedingUpdate");
        return JsonSerializer.Deserialize<List<AlpmPackageUpdateDto>>(json) ?? new List<AlpmPackageUpdateDto>();
    }

    public void InstallPackages(List<string> packageNames, AlpmTransFlag flags = AlpmTransFlag.NoScriptlet | AlpmTransFlag.NoHooks)
    {
        var jsonArgs = JsonSerializer.Serialize(packageNames);
        RunWorker("InstallPackages", $"'{jsonArgs}'");
    }

    public void RemovePackage(string packageName, AlpmTransFlag flags = AlpmTransFlag.NoScriptlet | AlpmTransFlag.NoHooks)
    {
        RunWorker("RemovePackage", packageName);
    }

    public void UpdatePackages(List<string> packageNames, AlpmTransFlag flags = AlpmTransFlag.NoScriptlet | AlpmTransFlag.NoHooks)
    {
        var jsonArgs = JsonSerializer.Serialize(packageNames);
        RunWorker("UpdatePackages", $"'{jsonArgs}'");
    }
}
