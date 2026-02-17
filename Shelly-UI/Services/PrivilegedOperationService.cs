using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using PackageManager.Alpm;
using Shelly_UI.Models;
using PackageManager.Aur.Models;
using Shelly.Utilities.System;
using Shelly_UI.Views;

namespace Shelly_UI.Services;

public class PrivilegedOperationService : IPrivilegedOperationService
{
    private readonly string _cliPath;
    private readonly ICredentialManager _credentialManager;
    private readonly IAlpmEventService _alpmEventService;
    private readonly IConfigService _configService;

    public PrivilegedOperationService(ICredentialManager credentialManager, IAlpmEventService alpmEventService,
        IConfigService configService)
    {
        _credentialManager = credentialManager;
        _alpmEventService = alpmEventService;
        _configService = configService;
        _cliPath = FindCliPath();
    }

    private string[] AppendNoConfirmIfNeeded(params string[] args)
    {
        var config = _configService.LoadConfig();
        if (config.NoConfirm)
        {
            return [..args, "--no-confirm"];
        }

        return args;
    }

    private Task<OperationResult> ExecutePrivilegedWithNoConfirmCheck(string operationDescription, params string[] args)
    {
        var finalArgs = AppendNoConfirmIfNeeded(args);
        return ExecutePrivilegedCommandAsync(operationDescription, finalArgs);
    }

    private static string FindCliPath()
    {
#if DEBUG
        var home = EnvironmentManager.UserPath;
        if (home == null)
        {
            throw new InvalidOperationException("HOME environment variable is not set.");
        }

        var debugPath =
            Path.Combine(home!, "RiderProjects/Shelly-ALPM/Shelly-CLI/bin/Debug/net10.0/linux-x64/shelly");
        Console.Error.WriteLine($"Debug path: {debugPath}");
#endif

        // Check common installation paths
        var possiblePaths = new[]
        {
#if DEBUG
            debugPath,
#endif
            "/usr/bin/shelly",
            "/usr/local/bin/shelly",
            Path.Combine(AppContext.BaseDirectory, "shelly"),
            Path.Combine(AppContext.BaseDirectory, "Shelly"),
            // Development path - relative to UI executable
            Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory) ?? "", "Shelly", "Shelly"),
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        // Fallback to assuming it's in PATH
        return "shelly";
    }

    public async Task<OperationResult> SyncDatabasesAsync()
    {
        return await ExecutePrivilegedCommandAsync("Synchronize package databases", "sync");
    }

    public async Task<List<AlpmPackageDto>> SearchPackagesAsync(string query)
    {
        var result = await ExecuteCommandAsync("list-available", $"--filter {query}",
            "--no-confirm", "--json");
        if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
        {
            return [];
        }

        try
        {
            // The output may contain multiple lines, find the JSON line
            var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmedLine = StripBom(line.Trim());
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    var packages = System.Text.Json.JsonSerializer.Deserialize(trimmedLine,
                        ShellyUIJsonContext.Default.ListAlpmPackageDto);
                    return packages ?? [];
                }
            }

            // If no JSON array found, try parsing the whole output
            var allPackages = System.Text.Json.JsonSerializer.Deserialize(StripBom(result.Output.Trim()),
                ShellyUIJsonContext.Default.ListAlpmPackageDto);
            return allPackages ?? [];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to parse available packages JSON: {ex.Message}");
            return [];
        }
    }

    public async Task<OperationResult> InstallPackagesAsync(IEnumerable<string> packages)
    {
        var packageArgs = string.Join(" ", packages);
        return await ExecutePrivilegedWithNoConfirmCheck("Install packages", "install", packageArgs);
    }

    public async Task<OperationResult> InstallLocalPackageAsync(string filePath)
    {
        return await ExecutePrivilegedWithNoConfirmCheck("Install local package", "install-local", "--location",
            filePath);
    }

    public async Task<OperationResult> RemovePackagesAsync(IEnumerable<string> packages)
    {
        var packageArgs = string.Join(" ", packages);
        return await ExecutePrivilegedWithNoConfirmCheck("Remove packages", "remove", packageArgs);
    }

    public async Task<OperationResult> UpdatePackagesAsync(IEnumerable<string> packages)
    {
        var packageArgs = string.Join(" ", packages);
        return await ExecutePrivilegedWithNoConfirmCheck("Update packages", "update", packageArgs);
    }

    public async Task<OperationResult> UpgradeSystemAsync()
    {
        return await ExecutePrivilegedWithNoConfirmCheck("Upgrade system", "upgrade");
    }

    public async Task<OperationResult> ForceSyncDatabaseAsync()
    {
        return await ExecutePrivilegedCommandAsync("Force synchronize package databases", "sync", "--force");
    }

    public async Task<OperationResult> InstallAurPackagesAsync(IEnumerable<string> packages)
    {
        var packageArgs = string.Join(" ", packages);
        return await ExecutePrivilegedWithNoConfirmCheck("Install AUR packages", "aur", "install", packageArgs);
    }

    public async Task<OperationResult> RemoveAurPackagesAsync(IEnumerable<string> packages)
    {
        var packageArgs = string.Join(" ", packages);
        return await ExecutePrivilegedWithNoConfirmCheck("Remove AUR packages", "aur", "remove", packageArgs);
    }

    public async Task<OperationResult> UpdateAurPackagesAsync(IEnumerable<string> packages)
    {
        var packageArgs = string.Join(" ", packages);
        return await ExecutePrivilegedWithNoConfirmCheck("Update AUR packages", "aur", "update", packageArgs);
    }

    public async Task<List<AlpmPackageUpdateDto>> GetPackagesNeedingUpdateAsync()
    {
        // Use privileged execution to sync databases and get updates
        var result = await ExecutePrivilegedCommandAsync("Check for Updates", "list-updates", "--json");

        if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
        {
            return [];
        }

        try
        {
            // The output may contain multiple lines, find the JSON line
            var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmedLine = StripBom(line.Trim());
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    var updates = System.Text.Json.JsonSerializer.Deserialize(trimmedLine,
                        ShellyUIJsonContext.Default.ListAlpmPackageUpdateDto);
                    return updates ?? [];
                }
            }

            // If no JSON array found, try parsing the whole output
            var allUpdates = System.Text.Json.JsonSerializer.Deserialize(StripBom(result.Output.Trim()),
                ShellyUIJsonContext.Default.ListAlpmPackageUpdateDto);
            return allUpdates ?? [];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to parse updates JSON: {ex.Message}");
            return [];
        }
    }

    public async Task<List<AlpmPackageDto>> GetAvailablePackagesAsync()
    {
        var result = await ExecuteCommandAsync("list-available", "--json");

        if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
        {
            return [];
        }

        try
        {
            // The output may contain multiple lines, find the JSON line
            var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmedLine = StripBom(line.Trim());
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    var packages = System.Text.Json.JsonSerializer.Deserialize(trimmedLine,
                        ShellyUIJsonContext.Default.ListAlpmPackageDto);
                    return packages ?? [];
                }
            }

            // If no JSON array found, try parsing the whole output
            var allPackages = System.Text.Json.JsonSerializer.Deserialize(StripBom(result.Output.Trim()),
                ShellyUIJsonContext.Default.ListAlpmPackageDto);
            return allPackages ?? [];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to parse available packages JSON: {ex.Message}");
            return [];
        }
    }

    public async Task<List<AlpmPackageDto>> GetInstalledPackagesAsync()
    {
        var result = await ExecuteCommandAsync("list-installed", "--json");

        if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
        {
            return [];
        }

        try
        {
            // The output may contain multiple lines, find the JSON line
            var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmedLine = StripBom(line.Trim());
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    var packages = System.Text.Json.JsonSerializer.Deserialize(trimmedLine,
                        ShellyUIJsonContext.Default.ListAlpmPackageDto);
                    return packages ?? [];
                }
            }

            // If no JSON array found, try parsing the whole output
            var allPackages = System.Text.Json.JsonSerializer.Deserialize(StripBom(result.Output.Trim()),
                ShellyUIJsonContext.Default.ListAlpmPackageDto);
            return allPackages ?? [];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to parse installed packages JSON: {ex.Message}");
            return [];
        }
    }

    public async Task<List<AurPackageDto>> GetAurInstalledPackagesAsync()
    {
        var result = await ExecuteCommandAsync("aur list-installed", "--json");

        if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
        {
            return [];
        }

        try
        {
            var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmedLine = StripBom(line.Trim());
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    var packages = System.Text.Json.JsonSerializer.Deserialize(trimmedLine,
                        ShellyUIJsonContext.Default.ListAurPackageDto);
                    return packages ?? [];
                }
            }

            // If no JSON array found, try parsing the whole output
            var allPackages = System.Text.Json.JsonSerializer.Deserialize(StripBom(result.Output.Trim()),
                ShellyUIJsonContext.Default.ListAurPackageDto);
            return allPackages ?? [];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to parse installed packages JSON: {ex.Message}");
            return [];
        }
    }

    public async Task<List<AurUpdateDto>> GetAurUpdatePackagesAsync()
    {
        var result = await ExecuteCommandAsync("aur list-updates", "--json");

        if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
        {
            return [];
        }

        try
        {
            var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmedLine = StripBom(line.Trim());
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    var packages = System.Text.Json.JsonSerializer.Deserialize(trimmedLine,
                        ShellyUIJsonContext.Default.ListAurUpdateDto);
                    return packages ?? [];
                }
            }

            // If no JSON array found, try parsing the whole output
            var allPackages = System.Text.Json.JsonSerializer.Deserialize(StripBom(result.Output.Trim()),
                ShellyUIJsonContext.Default.ListAurUpdateDto);
            return allPackages ?? [];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to parse installed packages JSON: {ex.Message}");
            return [];
        }
    }

    public async Task<List<AurPackageDto>> SearchAurPackagesAsync(string query)
    {
        var result = await ExecuteCommandAsync("aur search", query, "--json");

        if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
        {
            return [];
        }

        try
        {
            var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmedLine = StripBom(line.Trim());
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    var packages = System.Text.Json.JsonSerializer.Deserialize(trimmedLine,
                        ShellyUIJsonContext.Default.ListAurPackageDto);
                    return packages ?? [];
                }
            }

            // If no JSON array found, try parsing the whole output
            var allPackages = System.Text.Json.JsonSerializer.Deserialize(StripBom(result.Output.Trim()),
                ShellyUIJsonContext.Default.ListAurPackageDto);
            return allPackages ?? [];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to parse installed packages JSON: {ex.Message}");
            return [];
        }
    }

    public async Task<bool> IsPackageInstalledOnMachine(string packageName)
    {
        var aurPackages = await GetAurInstalledPackagesAsync();

        //Enable below statement if moved to standard package.
        //var standardPackages = await GetInstalledPackagesAsync();
        return aurPackages.Any(x => x.Name.Contains(packageName));
    }

    private async Task<OperationResult> ExecuteCommandAsync(params string[] args)
    {
        var arguments = string.Join(" ", args);
        var fullCommand = $"{_cliPath} {arguments}";

        Console.WriteLine($"Executing command: {fullCommand} --ui-mode");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _cliPath,
                Arguments = string.IsNullOrWhiteSpace(arguments) ? "--ui-mode" : arguments + " --ui-mode",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();

            // Read output and error streams synchronously to avoid race conditions
            // Use Task.WhenAll to read both streams concurrently
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(outputTask, errorTask);
            await process.WaitForExitAsync();

            var output = await outputTask;
            var error = await errorTask;

            // Log stderr for debugging
            if (!string.IsNullOrEmpty(error))
            {
                Console.Error.WriteLine(error);
            }

            return new OperationResult
            {
                Success = process.ExitCode == 0,
                Output = output,
                Error = error,
                ExitCode = process.ExitCode
            };
        }
        catch (Exception ex)
        {
            return new OperationResult
            {
                Success = false,
                Output = string.Empty,
                Error = ex.Message,
                ExitCode = -1
            };
        }
    }

    private async Task<OperationResult> ExecutePrivilegedCommandAsync(string operationDescription, params string[] args)
    {
        // Request credentials if not already available
        var hasCredentials = await _credentialManager.RequestCredentialsAsync(operationDescription);
        if (!hasCredentials)
        {
            return new OperationResult
            {
                Success = false,
                Output = string.Empty,
                Error = "Authentication cancelled by user.",
                ExitCode = -1
            };
        }

        var password = _credentialManager.GetPassword();
        if (string.IsNullOrEmpty(password))
        {
            return new OperationResult
            {
                Success = false,
                Output = string.Empty,
                Error = "No password available.",
                ExitCode = -1
            };
        }

        var arguments = string.Join(" ", args);
        var fullCommand = $"{_cliPath} {arguments}";

        Console.WriteLine($"Executing privileged command: sudo {fullCommand}");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "sudo",
                Arguments = $"-S {fullCommand} --ui-mode",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true
            }
        };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();
        StreamWriter? stdinWriter = null;

        // Semaphore + counter to prevent stdin from closing before async callbacks complete
        var stdinLock = new SemaphoreSlim(1, 1);
        bool stdinClosed = false;
        int pendingCallbacks = 0;
        var allCallbacksDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Helper to safely write to stdin
        async Task SafeWriteAsync(string value)
        {
            await stdinLock.WaitAsync();
            try
            {
                if (!stdinClosed && stdinWriter != null)
                {
                    await stdinWriter.WriteLineAsync(value);
                    await stdinWriter.FlushAsync();
                }
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                stdinLock.Release();
            }
        }

        // State for provider selection handling
        var providerOptions = new List<string>();
        string? providerQuestion = null;
        var awaitingProviderSelection = false;

        // State for conflict selection handling
        var conflictOptions = new List<string>();
        string? conflictQuestion = null;
        var awaitingConflictSelection = false;

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
                Console.WriteLine(e.Data);
            }
        };

        var passwordSent = false;
        var passwordSentLock = new object();
        
        process.ErrorDataReceived += async (sender, e) =>
        {
            if (e.Data != null)
            {
                // Detect sudo password prompt and respond to it
                if ((e.Data.Contains("[sudo]") || e.Data.Contains("password for")) && !passwordSent)
                {
                    lock (passwordSentLock)
                    {
                        if (!passwordSent)
                        {
                            passwordSent = true;
                            SafeWriteAsync(password).Wait();
                            Console.Error.WriteLine("[Shelly] Password sent to sudo");
                        }
                    }

                    return;
                }

                // Filter out password prompts from being logged
                if (e.Data.Contains("[sudo]") || e.Data.Contains("password for"))
                {
                    return;
                }

                Interlocked.Increment(ref pendingCallbacks);

                // Filter out the password prompt from sudo
                if (!e.Data.Contains("[sudo]") && !e.Data.Contains("password for"))
                {
                    Interlocked.Increment(ref pendingCallbacks);
                    try
                    {
                        // Handle provider selection protocol
                        if (e.Data.StartsWith("[Shelly][ALPM_SELECT_PROVIDER]"))
                        {
                            Console.Error.WriteLine($"[Shelly]Select provider for: {e.Data}");
                            awaitingProviderSelection = true;
                            providerOptions.Clear();
                            providerQuestion = e.Data.Substring("[Shelly][ALPM_SELECT_PROVIDER]".Length);
                            Console.Error.WriteLine($"[Shelly]Select provider for: {providerQuestion}");
                        }
                        else if (e.Data.StartsWith("[Shelly][ALPM_PROVIDER_OPTION]"))
                        {
                            Console.Error.WriteLine($"[Shelly]Provider option received: {e.Data}");
                            var payload = e.Data.Substring("[Shelly][ALPM_PROVIDER_OPTION]".Length);
                            var parts = payload.Split(':', 2);
                            if (parts.Length == 2 && int.TryParse(parts[0], out var idx))
                            {
                                // Ensure list size
                                while (providerOptions.Count <= idx) providerOptions.Add(string.Empty);
                                providerOptions[idx] = parts[1];
                            }
                            else
                            {
                                providerOptions.Add(payload);
                            }
                        }
                        else if (e.Data.StartsWith("[Shelly][ALPM_PROVIDER_END]"))
                        {
                            Console.Error.WriteLine($"[Shelly]Provider selection received");
                            var args = new QuestionEventArgs(
                                QuestionType.SelectProvider,
                                providerQuestion ?? "Select provider",
                                new List<string>(providerOptions),
                                providerQuestion);

                            _alpmEventService.RaiseQuestion(args);

                            await Task.Run(() => args.WaitForResponse());

                            await SafeWriteAsync(args.Response.ToString());
                            Console.Error.WriteLine($"[Shelly]Wrote selection {args.Response}");

                            awaitingProviderSelection = false;
                            providerQuestion = null;
                            providerOptions.Clear();
                        }
                        // Handle conflict selection protocol
                        else if (e.Data.StartsWith("[Shelly][ALPM_CONFLICT]"))
                        {
                            Console.Error.WriteLine($"[Shelly]Conflict question: {e.Data}");
                            awaitingConflictSelection = true;
                            conflictOptions.Clear();
                            conflictQuestion = e.Data.Substring("[Shelly][ALPM_CONFLICT]".Length);
                        }
                        else if (e.Data.StartsWith("[Shelly][ALPM_CONFLICT_OPTION]"))
                        {
                            Console.Error.WriteLine($"[Shelly]Conflict option received: {e.Data}");
                            var payload = e.Data.Substring("[Shelly][ALPM_CONFLICT_OPTION]".Length);
                            var parts = payload.Split(':', 2);
                            if (parts.Length == 2 && int.TryParse(parts[0], out var idx))
                            {
                                while (conflictOptions.Count <= idx) conflictOptions.Add(string.Empty);
                                conflictOptions[idx] = parts[1];
                            }
                            else
                            {
                                conflictOptions.Add(payload);
                            }
                        }
                        else if (e.Data.StartsWith("[Shelly][ALPM_CONFLICT_END]"))
                        {
                            Console.Error.WriteLine($"[Shelly]Conflict selection received");
                            var args = new QuestionEventArgs(
                                QuestionType.ConflictPkg,
                                conflictQuestion ?? "Package Conflict",
                                new List<string>(conflictOptions));

                            _alpmEventService.RaiseQuestion(args);

                            await Task.Run(() => args.WaitForResponse());

                            await SafeWriteAsync(args.Response.ToString());
                            Console.Error.WriteLine($"[Shelly]Wrote conflict selection {args.Response}");

                            awaitingConflictSelection = false;
                            conflictQuestion = null;
                            conflictOptions.Clear();
                        }
                        // Check for generic ALPM question (yes/no)
                        else if (e.Data.StartsWith("[Shelly][ALPM_QUESTION]"))
                        {
                            var questionText = e.Data.Substring("[Shelly][ALPM_QUESTION]".Length);
                            Console.Error.WriteLine($"[Shelly]Question received: {questionText}");

                            var args = new QuestionEventArgs(
                                QuestionType.InstallIgnorePkg,
                                questionText);

                            _alpmEventService.RaiseQuestion(args);

                            await Task.Run(() => args.WaitForResponse());

                            // Send response to CLI via stdin
                            await SafeWriteAsync(args.Response == 1 ? "y" : "n");
                        }
                        else
                        {
                            errorBuilder.AppendLine(e.Data);
                            Console.Error.WriteLine(e.Data);
                        }
                    }
                    finally
                    {
                        if (Interlocked.Decrement(ref pendingCallbacks) == 0)
                            allCallbacksDone.TrySetResult();
                    }
                }
            }
        };

        try
        {
            process.Start();
            stdinWriter = process.StandardInput;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await stdinWriter.FlushAsync();

            await process.WaitForExitAsync();

            // Wait for any in-flight async callbacks to finish writing
            if (Volatile.Read(ref pendingCallbacks) > 0)
            {
                await Task.WhenAny(allCallbacksDone.Task, Task.Delay(TimeSpan.FromMinutes(2)));
            }


            await stdinLock.WaitAsync();
            try
            {
                stdinClosed = true;
                stdinWriter?.Close();
            }
            finally
            {
                stdinLock.Release();
            }

            var success = process.ExitCode == 0;

            // Update credential validation status based on result
            if (success)
            {
                _credentialManager.MarkAsValidated();
            }
            else
            {
                // Check if it was an authentication failure
                var errorOutput = errorBuilder.ToString();
                if (errorOutput.Contains("incorrect password") ||
                    errorOutput.Contains("Sorry, try again") ||
                    errorOutput.Contains("Authentication failure") ||
                    process.ExitCode == 1 && errorOutput.Contains("sudo"))
                {
                    _credentialManager.MarkAsInvalid();
                }
            }

            return new OperationResult
            {
                Success = success,
                Output = outputBuilder.ToString(),
                Error = errorBuilder.ToString(),
                ExitCode = process.ExitCode
            };
        }
        catch (Exception ex)
        {
            return new OperationResult
            {
                Success = false,
                Output = string.Empty,
                Error = ex.Message,
                ExitCode = -1
            };
        }
    }

    /// <summary>
    /// Strips UTF-8 BOM (Byte Order Mark) from the beginning of a string if present.
    /// </summary>
    private static string StripBom(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // UTF-8 BOM is 0xEF 0xBB 0xBF which appears as \uFEFF in .NET strings
        return input.TrimStart('\uFEFF');
    }
}