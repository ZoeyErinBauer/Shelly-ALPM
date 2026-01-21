using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
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
