using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Shelly_UI.Services.TrayService;

public class TrayService : ITrayService
{
    private readonly IUnprivilegedOperationService _unprivilegedOperationService;
    private CancellationTokenSource? _cts;
    private Task? _backgroundTask;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(120);

    public TrayService(IUnprivilegedOperationService unprivilegedOperationService)
    {
        _unprivilegedOperationService = unprivilegedOperationService;
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
                    await Task.Delay(CheckInterval, token);
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
