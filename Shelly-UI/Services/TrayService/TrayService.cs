using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Shelly_UI.Services.TrayService;

public class TrayService : ITrayService
{
    private readonly IUnprivilegedOperationService _unprivilegedOperationService;
    private readonly IConfigService _configService;
    private CancellationTokenSource? _cts;
    private Task? _backgroundTask;

    public TrayService(IUnprivilegedOperationService unprivilegedOperationService, IConfigService configService)
    {
        _unprivilegedOperationService = unprivilegedOperationService;
        _configService = configService;
    }

    public void Start()
    {
        if (_cts is not null)
            return;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _backgroundTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await CheckForUpdates();
                }
                catch (Exception)
                {
                    // Swallow exceptions so the loop continues
                }

                try
                {
                    var config = _configService.LoadConfig();
                    var checkInterval = TimeSpan.FromHours(config.TrayCheckIntervalHours);
                    await Task.Delay(checkInterval, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }, token);
    }

    public async Task CheckForUpdates()
    {
        var syncModel = await _unprivilegedOperationService.CheckForApplicationUpdates();

        var updateCount = syncModel.Packages.Count
                        + syncModel.Aur.Count
                        + syncModel.Flatpaks.Count;

        if (updateCount > 0)
        {
            SendNotification($"{updateCount} update{(updateCount > 1 ? "s" : "")} available",
                "Run Shelly to update your packages.");
        }
    }

    private static void SendNotification(string summary, string body)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "notify-send",
                ArgumentList = { "-a", "Shelly", summary, body },
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            process.Start();
        }
        catch (Exception)
        {
            // notify-send may not be available; silently ignore
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _backgroundTask = null;
    }

    public void Dispose()
    {
        Stop();
    }
}
