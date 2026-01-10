using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace PackageManager.Alpm;

public class AlpmWorkerClient : IAlpmManager, IDisposable
{
    private readonly string _workerPath;
    private readonly Func<string?>? _passwordProvider;
    private Process? _workerProcess;
    private StreamWriter? _workerInput;
    private StreamReader? _workerOutput;
    private StreamReader? _workerError;

    public AlpmWorkerClient(string workerPath, Func<string?>? passwordProvider = null)
    {
        _workerPath = workerPath;
        _passwordProvider = passwordProvider;
    }

    private void EnsureWorkerStarted()
    {
        if (_workerProcess != null && !_workerProcess.HasExited)
        {
            return;
        }

        var password = _passwordProvider?.Invoke();

        var processInfo = new ProcessStartInfo
        {
            FileName = "sudo",
            Arguments = $"-S {_workerPath}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _workerProcess = Process.Start(processInfo) ?? throw new Exception("Failed to start worker process.");
        _workerInput = _workerProcess.StandardInput;
        _workerOutput = _workerProcess.StandardOutput;
        _workerError = _workerProcess.StandardError;

        if (!string.IsNullOrEmpty(password))
        {
            _workerInput.WriteLine(password);
            _workerInput.Flush();
        }
        else
        {
            throw new Exception("Authentication required: No password provided.");
        }
    }

    private string RunWorker(string command, string? payload = null)
    {
        EnsureWorkerStarted();

        var request = new WorkerRequest
        {
            Command = command,
            Payload = payload
        };

        var jsonRequest = JsonSerializer.Serialize(request);
        _workerInput!.WriteLine(jsonRequest);
        _workerInput.Flush();

        var jsonResponse = _workerOutput!.ReadLine();
        if (jsonResponse == null)
        {
            var error = _workerError!.ReadToEnd();
            throw new Exception($"Worker process exited unexpectedly: {error}");
        }

        var response = JsonSerializer.Deserialize<WorkerResponse>(jsonResponse)
                       ?? throw new Exception("Failed to deserialize worker response.");

        if (!response.Success)
        {
            throw new Exception($"Worker error: {response.Error}");
        }

        return response.Data ?? string.Empty;
    }

    public void IntializeWithSync() => RunWorker("Sync");

    public void Initialize()
    {
        /* Worker initializes per command in this implementation or we could add an Init command */
    }

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

    public void InstallPackages(List<string> packageNames,
        AlpmTransFlag flags = AlpmTransFlag.NoScriptlet | AlpmTransFlag.NoHooks)
    {
        var jsonArgs = JsonSerializer.Serialize(packageNames);
        RunWorker("InstallPackages", jsonArgs);
    }

    public void RemovePackages(List<string> packageNames,
        AlpmTransFlag flags = AlpmTransFlag.NoScriptlet | AlpmTransFlag.NoHooks)
    {
        var jsonArgs = JsonSerializer.Serialize(packageNames);
        RunWorker("RemovePackages", jsonArgs);
    }

    public void RemovePackage(string packageName,
        AlpmTransFlag flags = AlpmTransFlag.NoScriptlet | AlpmTransFlag.NoHooks)
    {
        RunWorker("RemovePackage", packageName);
    }

    public void UpdatePackages(List<string> packageNames,
        AlpmTransFlag flags = AlpmTransFlag.NoScriptlet | AlpmTransFlag.NoHooks)
    {
        var jsonArgs = JsonSerializer.Serialize(packageNames);
        RunWorker("UpdatePackages", jsonArgs);
    }

    public void Dispose()
    {
        if (_workerProcess != null && !_workerProcess.HasExited)
        {
            try
            {
                var request = new WorkerRequest { Command = "Exit" };
                var jsonRequest = JsonSerializer.Serialize(request);
                _workerInput?.WriteLine(jsonRequest);
                _workerInput?.Flush();
                if (!_workerProcess.WaitForExit(1000))
                {
                    _workerProcess.Kill();
                }
            }
            catch
            {
                _workerProcess.Kill();
            }
            finally
            {
                _workerProcess.Dispose();
            }
        }
    }
}