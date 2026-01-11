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

    private bool _isElevated;

    public AlpmWorkerClient(string workerPath, Func<string?>? passwordProvider = null)
    {
        _workerPath = workerPath;
        _passwordProvider = passwordProvider;
    }

    private void EnsureWorkerStarted(bool elevated)
    {
        if (_workerProcess != null)
        {
            if (!_workerProcess.HasExited && (_isElevated == elevated || (_isElevated && !elevated)))
            {
                return;
            }

            // If we need elevation but the current process isn't elevated, or it has exited, we need to restart it.
            StopWorker();
        }

        var processInfo = new ProcessStartInfo
        {
            FileName = elevated ? "sudo" : _workerPath,
            Arguments = elevated ? $"-S {_workerPath}" : "",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            _workerProcess = Process.Start(processInfo) ?? throw new Exception("Failed to start worker process.");
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.Message.Contains("No such file or directory"))
        {
             // Fallback for development/test if workerPath is not absolute and not in PATH
             if (!Path.IsPathRooted(_workerPath))
             {
                 var absolutePath = Path.GetFullPath(_workerPath);
                 if (File.Exists(absolutePath))
                 {
                     processInfo.FileName = elevated ? "sudo" : absolutePath;
                     processInfo.Arguments = elevated ? $"-S {absolutePath}" : "";
                     _workerProcess = Process.Start(processInfo) ?? throw new Exception("Failed to start worker process.");
                 }
                 else
                 {
                     throw;
                 }
             }
             else
             {
                 throw;
             }
        }

        _workerInput = _workerProcess.StandardInput;
        _workerOutput = _workerProcess.StandardOutput;
        _isElevated = elevated;

        // Subscribe to standard error to log output
        _workerProcess.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Console.WriteLine($"[WORKER LOG] {e.Data}");
            }
        };
        _workerProcess.BeginErrorReadLine();

        if (elevated)
        {
            var password = _passwordProvider?.Invoke();
            if (!string.IsNullOrEmpty(password))
            {
                _workerInput.WriteLine(password);
                _workerInput.Flush();
            }
            else
            {
                // We might want to allow sudo without password if configured, but usually, we need it.
                // For now, let's keep it required as per original logic.
                throw new Exception("Authentication required: No password provided.");
            }
        }
    }

    private void StopWorker()
    {
        if (_workerProcess != null && !_workerProcess.HasExited)
        {
            try
            {
                var request = new WorkerRequest { Command = "Exit" };
                var jsonRequest = JsonSerializer.Serialize(request, AlpmWorkerJsonContext.Default.WorkerRequest);
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
                _workerProcess = null;
                _workerInput = null;
                _workerOutput = null;
            }
        }
    }

    private string RunWorker(string command, string? payload = null, bool elevated = false)
    {
        EnsureWorkerStarted(elevated);

        var request = new WorkerRequest
        {
            Command = command,
            Payload = payload
        };

        var jsonRequest = JsonSerializer.Serialize(request, AlpmWorkerJsonContext.Default.WorkerRequest);
        _workerInput!.WriteLine(jsonRequest);
        _workerInput.Flush();

        var jsonResponse = _workerOutput!.ReadLine();
        if (jsonResponse == null)
        {
            throw new Exception("Worker process exited unexpectedly.");
        }

        var response = JsonSerializer.Deserialize(jsonResponse, AlpmWorkerJsonContext.Default.WorkerResponse)
                       ?? throw new Exception("Failed to deserialize worker response.");

        if (!response.Success)
        {
            throw new Exception($"Worker error: {response.Error}");
        }

        return response.Data ?? string.Empty;
    }

    public void IntializeWithSync() => RunWorker("Sync", elevated: true);

    public void Initialize()
    {
        /* Worker initializes per command in this implementation or we could add an Init command */
    }

    public void Sync(bool force = false) => RunWorker("Sync", elevated: true);

    public List<AlpmPackageDto> GetInstalledPackages()
    {
        var json = RunWorker("GetInstalledPackages", elevated: false);
        return JsonSerializer.Deserialize(json, AlpmWorkerJsonContext.Default.ListAlpmPackageDto) ?? new List<AlpmPackageDto>();
    }

    public List<AlpmPackageDto> GetAvailablePackages()
    {
        var json = RunWorker("GetAvailablePackages", elevated: false);
        return JsonSerializer.Deserialize(json, AlpmWorkerJsonContext.Default.ListAlpmPackageDto) ?? new List<AlpmPackageDto>();
    }

    public List<AlpmPackageUpdateDto> GetPackagesNeedingUpdate()
    {
        var json = RunWorker("GetPackagesNeedingUpdate", elevated: false);
        return JsonSerializer.Deserialize(json, AlpmWorkerJsonContext.Default.ListAlpmPackageUpdateDto) ?? new List<AlpmPackageUpdateDto>();
    }

    public void InstallPackages(List<string> packageNames,
        AlpmTransFlag flags = AlpmTransFlag.NoScriptlet | AlpmTransFlag.NoHooks)
    {
        var jsonArgs = JsonSerializer.Serialize(packageNames, AlpmWorkerJsonContext.Default.ListString);
        RunWorker("InstallPackages", jsonArgs, elevated: true);
    }

    public void RemovePackages(List<string> packageNames,
        AlpmTransFlag flags = AlpmTransFlag.NoScriptlet | AlpmTransFlag.NoHooks)
    {
        var jsonArgs = JsonSerializer.Serialize(packageNames, AlpmWorkerJsonContext.Default.ListString);
        RunWorker("RemovePackages", jsonArgs, elevated: true);
    }

    public void RemovePackage(string packageName,
        AlpmTransFlag flags = AlpmTransFlag.NoScriptlet | AlpmTransFlag.NoHooks)
    {
        RunWorker("RemovePackage", packageName, elevated: true);
    }

    public void UpdatePackages(List<string> packageNames,
        AlpmTransFlag flags = AlpmTransFlag.NoScriptlet | AlpmTransFlag.NoHooks)
    {
        var jsonArgs = JsonSerializer.Serialize(packageNames, AlpmWorkerJsonContext.Default.ListString);
        RunWorker("UpdatePackages", jsonArgs, elevated: true);
    }

    public void SyncSystemUpdate(AlpmTransFlag flags = AlpmTransFlag.NoHooks | AlpmTransFlag.NoScriptlet)
    {
        EnsureWorkerStarted(true);
        RunWorker("SyncSystemUpdate", elevated: true); 
    }

    public void Dispose()
    {
        StopWorker();
    }
}