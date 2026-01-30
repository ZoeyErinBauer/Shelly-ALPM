using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using PackageManager.Alpm;
using Shelly.Utilities.System;
using Shelly_UI.Views;

namespace Shelly_UI.Services;

public class PrivilegedOperationService : IPrivilegedOperationService
{
    private readonly string _cliPath;
    private readonly ICredentialManager _credentialManager;

    public PrivilegedOperationService(ICredentialManager credentialManager)
    {
        _credentialManager = credentialManager;
        _cliPath = FindCliPath();
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
            Path.Combine(home!,"RiderProjects/Shelly-ALPM/Shelly-CLI/bin/Debug/net10.0/linux-x64/shelly");
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

    public async Task<OperationResult> InstallPackagesAsync(IEnumerable<string> packages)
    {
        var packageArgs = string.Join(" ", packages);
        return await ExecutePrivilegedCommandAsync("Install packages", "install", "--no-confirm", packageArgs);
    }

    public async Task<OperationResult> RemovePackagesAsync(IEnumerable<string> packages)
    {
        var packageArgs = string.Join(" ", packages);
        return await ExecutePrivilegedCommandAsync("Remove packages", "remove", "--no-confirm", packageArgs);
    }

    public async Task<OperationResult> UpdatePackagesAsync(IEnumerable<string> packages)
    {
        var packageArgs = string.Join(" ", packages);
        return await ExecutePrivilegedCommandAsync("Update packages", "update", "--no-confirm", packageArgs);
    }

    public async Task<OperationResult> UpgradeSystemAsync()
    {
        return await ExecutePrivilegedCommandAsync("Upgrade system", "upgrade", "--no-confirm");
    }

    public async  Task<OperationResult> ForceSyncDatabaseAsync()
    {
        return await ExecutePrivilegedCommandAsync("Force synchronize package databases", "sync", "--force");
        
    }
    
    public async Task<OperationResult> InstallAurPackagesAsync(IEnumerable<string> packages)
    {
        var packageArgs = string.Join(" ", packages);
        return await ExecutePrivilegedCommandAsync("Install AUR packages", "aur", "install", packageArgs);
    }
    
    public async Task<OperationResult> RemoveAurPackagesAsync(IEnumerable<string> packages)
    {
        var packageArgs = string.Join(" ", packages);
        return await ExecutePrivilegedCommandAsync("Remove AUR packages", "aur", "remove", packageArgs);
    }
    
    public async Task<OperationResult> UpdateAurPackagesAsync(IEnumerable<string> packages)
    {
        var packageArgs = string.Join(" ", packages);
        return await ExecutePrivilegedCommandAsync("Update AUR packages", "aur", "update", "--no-confirm", packageArgs);
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
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    var updates = System.Text.Json.JsonSerializer.Deserialize(trimmedLine, ShellyUIJsonContext.Default.ListAlpmPackageUpdateDto);
                    return updates ?? [];
                }
            }
            
            // If no JSON array found, try parsing the whole output
            var allUpdates = System.Text.Json.JsonSerializer.Deserialize(result.Output.Trim(), ShellyUIJsonContext.Default.ListAlpmPackageUpdateDto);
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
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    var packages = System.Text.Json.JsonSerializer.Deserialize(trimmedLine, ShellyUIJsonContext.Default.ListAlpmPackageDto);
                    return packages ?? [];
                }
            }
            
            // If no JSON array found, try parsing the whole output
            var allPackages = System.Text.Json.JsonSerializer.Deserialize(result.Output.Trim(), ShellyUIJsonContext.Default.ListAlpmPackageDto);
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
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    var packages = System.Text.Json.JsonSerializer.Deserialize(trimmedLine, ShellyUIJsonContext.Default.ListAlpmPackageDto);
                    return packages ?? [];
                }
            }
            
            // If no JSON array found, try parsing the whole output
            var allPackages = System.Text.Json.JsonSerializer.Deserialize(result.Output.Trim(), ShellyUIJsonContext.Default.ListAlpmPackageDto);
            return allPackages ?? [];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to parse installed packages JSON: {ex.Message}");
            return [];
        }
    }

    private async Task<OperationResult> ExecuteCommandAsync(params string[] args)
    {
        var arguments = string.Join(" ", args);
        var fullCommand = $"{_cliPath} {arguments}";

        Console.WriteLine($"Executing command: {fullCommand}");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _cliPath,
                Arguments = arguments,
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
        var fullCommand = $"{_cliPath} --ui-mode {arguments}";

        Console.WriteLine($"Executing privileged command: sudo {fullCommand}");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "sudo",
                Arguments = $"-S {fullCommand}",
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

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
                Console.WriteLine(e.Data);
            }
        };

        process.ErrorDataReceived += async (sender, e) =>
        {
            if (e.Data != null)
            {
                // Filter out the password prompt from sudo
                if (!e.Data.Contains("[sudo]") && !e.Data.Contains("password for"))
                {
                    // Check for ALPM question (with Shelly prefix)
                    if (e.Data.StartsWith("[Shelly][ALPM_QUESTION]"))
                    {
                        var questionText = e.Data.Substring("[Shelly][ALPM_QUESTION]".Length);
                        Console.Error.WriteLine($"[Shelly]Question received: {questionText}");
                        
                        // Show dialog on UI thread and get response
                        var response = await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop 
                                && desktop.MainWindow != null)
                            {
                                var dialog = new QuestionDialog(questionText);
                                var result = await dialog.ShowDialog<bool>(desktop.MainWindow);
                                return result;
                            }
                            return true; // Default to yes if no window available
                        });
                        
                        // Send response to CLI via stdin
                        if (stdinWriter != null)
                        {
                            await stdinWriter.WriteLineAsync(response ? "y" : "n");
                            await stdinWriter.FlushAsync();
                        }
                    }
                    else
                    {
                        errorBuilder.AppendLine(e.Data);
                        Console.Error.WriteLine(e.Data);
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

            // Write password to stdin followed by newline
            await stdinWriter.WriteLineAsync(password);
            await stdinWriter.FlushAsync();

            await process.WaitForExitAsync();
            
            // Close stdin after process exits
            stdinWriter.Close();

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
}
