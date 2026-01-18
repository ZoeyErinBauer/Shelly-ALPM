using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shelly.Protocol;
using Tmds.DBus.Protocol;

namespace Shelly.Service;

/// <summary>
/// Hosted service that manages the D-Bus connection and registers the Shelly service.
/// </summary>
public class ShellyDbusHostedService : IHostedService, IDisposable
{
    private readonly ShellyDbusService _shellyService;
    private readonly ILogger<ShellyDbusHostedService> _logger;
    private Connection? _connection;

    public ShellyDbusHostedService(ShellyDbusService shellyService, ILogger<ShellyDbusHostedService> logger)
    {
        _shellyService = shellyService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Shelly D-Bus service...");

        try
        {
            // Connect to the system bus (requires root privileges)
            _connection = new Connection(Address.System!);
            await _connection.ConnectAsync();

            _logger.LogInformation("Connected to system D-Bus as {UniqueName}", _connection.UniqueName);

            // Register the service object as a method handler
            _connection.AddMethodHandler(_shellyService);
            _logger.LogInformation("Registered service at path: {ObjectPath}", _shellyService.Path);

            _logger.LogInformation("Shelly D-Bus service started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Shelly D-Bus service");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Shelly D-Bus service...");

        _connection?.Dispose();
        _connection = null;

        _logger.LogInformation("Shelly D-Bus service stopped");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
