using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Reflection;
using Shelly.Gtk.Services.TrayServices;
using Gtk;
using Shelly.Gtk.Helpers;
using Shelly.Gtk.UiModels;

namespace Shelly.Gtk.Services;

public class GitHubUpdateService : IUpdateService
{
    private readonly HttpClient _httpClient;
    private const string RepoOwner = "ZoeyErinBauer";
    private const string RepoName = "Shelly-ALPM";
    private const string Url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
    private const string Url2 = $"https://api.github.com/repos/ZC-Development/Shelly/releases/latest";
    private GitHubRelease? _latestRelease;
    private ICredentialManager _credentialManager;

    public GitHubUpdateService(ICredentialManager credentialManager)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Shelly-ALPM-Updater");
        _credentialManager = credentialManager;
    }

    public async Task<bool> CheckForUpdateAsync()
    {
        try
        {
            await Console.Error.WriteLineAsync("[DEBUG] Checking for updates...");
            await Console.Error.WriteLineAsync($"[DEBUG] URL: {Url}");
            _latestRelease = await _httpClient.GetFromJsonAsync(Url, ShellyGtkJsonContext.Default.GitHubRelease) ??
                             await _httpClient.GetFromJsonAsync(Url2, ShellyGtkJsonContext.Default.GitHubRelease);
            await Console.Error.WriteLineAsync($"[DEBUG] Latest release: {_latestRelease?.TagName}");
            if (_latestRelease == null) return false;

            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
            Console.Error.WriteLine($"[DEBUG] Current version: {currentVersion?.ToString(3)}");
            if (currentVersion == null) return false;

            var versionString = _latestRelease.TagName.TrimStart('v');
            var dashIndex = versionString.IndexOf('-');
            if (dashIndex != -1)
            {
                versionString = versionString.Substring(0, dashIndex);
            }

            if (Version.TryParse(versionString, out var latestVersion))
            {
                Console.Error.WriteLine($"[DEBUG] Current version: {currentVersion}, Latest version: {latestVersion}");
                // Normalize both versions to 3 components (Major.Minor.Build) for comparison
                // This avoids issues where 1.4.0 (Revision=-1) is incorrectly considered less than 1.4.0.0 (Revision=0)
                var normalizedLatest = new Version(latestVersion.Major, latestVersion.Minor,
                    Math.Max(0, latestVersion.Build));
                var normalizedCurrent = new Version(currentVersion.Major, currentVersion.Minor,
                    Math.Max(0, currentVersion.Build));
                return normalizedLatest > normalizedCurrent;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking for updates: {ex.Message}");
        }

        return false;
    }

    public async Task<string> PullReleaseNotesAsync()
    {
        try
        {
            await Console.Error.WriteLineAsync("[DEBUG] Checking for updates...");
            await Console.Error.WriteLineAsync($"[DEBUG] URL: {Url}");
            _latestRelease = await _httpClient.GetFromJsonAsync(Url, ShellyGtkJsonContext.Default.GitHubRelease) ??
                             await _httpClient.GetFromJsonAsync(Url2, ShellyGtkJsonContext.Default.GitHubRelease);
            await Console.Error.WriteLineAsync($"[DEBUG] Latest release: {_latestRelease?.TagName}");

            return _latestRelease?.Body ?? string.Empty;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking for updates: {ex.Message}");
        }

        return string.Empty;
    }

    public async Task DownloadAndInstallUpdateAsync()
    {
        if (_latestRelease == null)
        {
            if (!await CheckForUpdateAsync() || _latestRelease == null)
                return;
        }

        var asset = _latestRelease.Assets.FirstOrDefault(x => x.Name.EndsWith(".tar.gz") || x.Name.EndsWith(".zip"));

        if (asset == null)
        {
            Console.WriteLine("No suitable update asset found.");
            return;
        }

        var tempPath = Path.Combine(Path.GetTempPath(), asset.Name);
        var extractPath = Path.Combine(Path.GetTempPath(), "ShellyUpdate");

        if (Directory.Exists(extractPath))
            Directory.Delete(extractPath, true);
        Directory.CreateDirectory(extractPath);

        try
        {
            Console.WriteLine($"Downloading update from {asset.BrowserDownloadUrl}...");
            var data = await _httpClient.GetByteArrayAsync(asset.BrowserDownloadUrl);
            await File.WriteAllBytesAsync(tempPath, data);

            Console.WriteLine("Extracting update...");
            if (asset.Name.Contains(".tar.gz") || tempPath.EndsWith(".tar.gz"))
            {
                await ExtractTarGz(tempPath, extractPath);
            }
            else
            {
                ZipFile.ExtractToDirectory(tempPath, extractPath);
            }

            Console.WriteLine("Installing update...");
            await RunInstallScript(extractPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Update failed: {ex.Message}");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private async Task ExtractTarGz(string tarGzPath, string extractPath)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "tar",
                Arguments = $"-xzf \"{tarGzPath}\" -C \"{extractPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new Exception($"Tar extraction failed: {error}");
        }
    }

    private async Task RunInstallScript(string extractPath)
    {
        var installScript = Path.Combine(extractPath, "install.sh");
        if (!File.Exists(installScript))
        {
            Console.WriteLine("No install.sh found in update package.");
            return;
        }

        // Get credentials via PrivilegedOperationService's credential manager

        var hasCredentials = await _credentialManager.RequestCredentialsAsync("Install Shelly update");
        if (!hasCredentials)
        {
            Console.WriteLine("Update cancelled: Authentication cancelled by user.");
            return;
        }

        var password = _credentialManager.GetPassword();
        if (string.IsNullOrEmpty(password))
        {
            Console.WriteLine("Update cancelled: No password available.");
            return;
        }

        TrayStartService.End();

        // Delay so it doesn't open in the wrong location
        await Task.Delay(100);


        var uiXml = ResourceHelper.LoadUiFile("UiFiles/Dialogs/UpdateProgressDialog.ui");
        var builder = Builder.NewFromString(uiXml, uiXml.Length);
        var window = (Window)builder.GetObject("UpdateProgressDialog")!;
        var statusLabel = (Label)builder.GetObject("status_label")!;
        var progressBar = (ProgressBar)builder.GetObject("progress_bar")!;

        window.SetDefaultSize(400, 200);
        progressBar.Pulse();

        // Pulse the progress bar periodically
        var pulsing = true;
        GLib.Functions.TimeoutAdd(0, 100, () =>
        {
            if (pulsing)
                progressBar.Pulse();
            return pulsing;
        });

        window.Show();

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "sudo",
                Arguments = $"-S bash \"{installScript}\"",
                WorkingDirectory = extractPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true
            }
        };

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                GLib.Functions.IdleAdd(0, () =>
                {
                    statusLabel.SetLabel(e.Data);
                    return false;
                });
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                // Filter out sudo password prompt
                if (!e.Data.Contains("[sudo]") && !e.Data.Contains("password for"))
                {
                    GLib.Functions.IdleAdd(0, () =>
                    {
                        statusLabel.SetLabel(e.Data);
                        return false;
                    });
                }
            }
        };

        process.Start();

        await process.StandardInput.WriteLineAsync(password);
        await process.StandardInput.FlushAsync();

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        pulsing = false;

        var success = process.ExitCode == 0;

        GLib.Functions.IdleAdd(0, () =>
        {
            progressBar.SetFraction(1.0);
            statusLabel.SetLabel(success ? "Update completed successfully!" : "Update failed.");
            window.SetTitle(success ? "Update Complete" : "Update Failed");
            return false;
        });

        // Wait for user to close the dialog
        var tcs = new TaskCompletionSource();
        window.OnCloseRequest += (_, _) =>
        {
            tcs.TrySetResult();
            return false;
        };

        await tcs.Task;

        if (!success)
        {
            throw new Exception($"Installation script failed with exit code: {process.ExitCode}");
        }
    }
}