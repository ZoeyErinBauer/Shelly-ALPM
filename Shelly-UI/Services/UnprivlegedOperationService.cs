using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Shelly_UI.Views;
using Shelly.Utilities.System;

namespace Shelly_UI.Services;

public class UnprivilegedOperationService : IUnprivilegedOperationService
{
    private readonly string _cliPath;

    public UnprivilegedOperationService()
    {
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

    public async Task<UnprivilegedOperationResult> ListFlatpakPackages()
    {
        return await ExecuteUnprivilegedCommandAsync("List packages", "flatpak list");
    }
    
    public async Task<UnprivilegedOperationResult> ListFlatpakUpdates()
    {
        return await ExecuteUnprivilegedCommandAsync("List packages", "flatpak list-updates");
    }

    public async Task<UnprivilegedOperationResult> RemoveFlatpakPackage(IEnumerable<string> packages)
    {
        var packageArgs = string.Join(" ", packages);
        return await ExecuteUnprivilegedCommandAsync("Install packages", "flatpak remove", packageArgs);
    }


    private async Task<UnprivilegedOperationResult> ExecuteUnprivilegedCommandAsync(string operationDescription,
        params string[] args)
    {
        var arguments = string.Join(" ", args) + " --json";
        var fullCommand = $"{_cliPath} {arguments}";

        Console.WriteLine($"Executing privileged command: {fullCommand}");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _cliPath,
                Arguments = arguments,
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
        };

        try
        {
            process.Start();
            stdinWriter = process.StandardInput;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            // Close stdin after process exits
            stdinWriter.Close();

            var success = process.ExitCode == 0;
            
            return new UnprivilegedOperationResult
            {
                Success = success,
                Output = outputBuilder.ToString(),
                Error = errorBuilder.ToString(),
                ExitCode = process.ExitCode
            };
        }
        catch (Exception ex)
        {
            return new UnprivilegedOperationResult
            {
                Success = false,
                Output = string.Empty,
                Error = ex.Message,
                ExitCode = -1
            };
        }
    }
}