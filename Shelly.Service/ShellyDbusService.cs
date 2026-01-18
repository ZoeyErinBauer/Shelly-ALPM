using System.Text.Json;
using Microsoft.Extensions.Logging;
using PackageManager.Alpm;
using Shelly.Protocol;
using Tmds.DBus.Protocol;

namespace Shelly.Service;

/// <summary>
/// D-Bus service implementation that wraps AlpmManager for privileged package operations.
/// This service runs as root and exposes package management functionality over D-Bus.
/// </summary>
public class ShellyDbusService : IMethodHandler
{
    private readonly IAlpmManager _alpmManager;
    private readonly ILogger<ShellyDbusService> _logger;

    public string Path => ShellyDbusConstants.ObjectPath;

    public ShellyDbusService(IAlpmManager alpmManager, ILogger<ShellyDbusService> logger)
    {
        _alpmManager = alpmManager;
        _logger = logger;
    }

    public bool RunMethodHandlerSynchronously(Message message) => false;

    public ValueTask HandleMethodAsync(MethodContext context)
    {
        _logger.LogInformation("Handling method call: {MethodName}", context.Request.MemberAsString);
        var request = context.Request;

        if (request.InterfaceAsString != ShellyDbusConstants.InterfaceName)
        {
            _logger.LogError("Unknown interface: {Interface}", request.InterfaceAsString);
            context.ReplyError("org.freedesktop.DBus.Error.UnknownInterface",
                $"Unknown interface: {request.InterfaceAsString}");
            return ValueTask.CompletedTask;
        }

        return request.MemberAsString switch
        {
            "InitializeWithSync" => HandleInitializeWithSyncAsync(context),
            "Initialize" => HandleInitializeAsync(context),
            "Sync" => HandleSyncAsync(context),
            "GetInstalledPackages" => HandleGetInstalledPackagesAsync(context),
            "GetAvailablePackages" => HandleGetAvailablePackagesAsync(context),
            "GetPackagesNeedingUpdate" => HandleGetPackagesNeedingUpdateAsync(context),
            "InstallPackages" => HandleInstallPackagesAsync(context),
            "RemovePackages" => HandleRemovePackagesAsync(context),
            "UpdatePackages" => HandleUpdatePackagesAsync(context),
            "SyncSystemUpdate" => HandleSyncSystemUpdateAsync(context),
            "Release" => HandleReleaseAsync(context),
            _ => HandleUnknownMethod(context)
        };
    }

    private async ValueTask HandleReleaseAsync(MethodContext context)
    {
        await Task.Run(() => _alpmManager.ReleaseHandle());
        ReplyEmpty(context);
    }

    private ValueTask HandleUnknownMethod(MethodContext context)
    {
        _logger.LogInformation("Unknown method called: {MethodName}", context.Request.MemberAsString);
        context.ReplyError("org.freedesktop.DBus.Error.UnknownMethod",
            $"Unknown method: {context.Request.MemberAsString}");
        return ValueTask.CompletedTask;
    }

    private void ReplyEmpty(MethodContext context)
    {
        using var writer = context.CreateReplyWriter(null);
        context.Reply(writer.CreateMessage());
    }

    private async ValueTask HandleInitializeWithSyncAsync(MethodContext context)
    {
        try
        {
            _logger.LogInformation("Initializing ALPM with sync");
            await Task.Run(() => _alpmManager.IntializeWithSync());
            ReplyEmpty(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize with sync");
            context.ReplyError("org.shelly.Error.InitializeFailed", ex.Message);
        }
    }

    private async ValueTask HandleInitializeAsync(MethodContext context)
    {
        try
        {
            _logger.LogInformation("Initializing ALPM");
            await Task.Run(() => _alpmManager.Initialize());
            ReplyEmpty(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize");
            context.ReplyError("org.shelly.Error.InitializeFailed", ex.Message);
        }
    }

    private async ValueTask HandleSyncAsync(MethodContext context)
    {
        try
        {
            var reader = context.Request.GetBodyReader();
            var force = reader.ReadBool();

            _logger.LogInformation("Syncing databases (force: {Force})", force);
            await Task.Run(() => _alpmManager.Sync(force));
            ReplyEmpty(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync");
            context.ReplyError("org.shelly.Error.SyncFailed", ex.Message);
        }
    }

    private async ValueTask HandleGetInstalledPackagesAsync(MethodContext context)
    {
        try
        {
            _logger.LogInformation("Getting installed packages");
            var packages = await Task.Run(() => _alpmManager.GetInstalledPackages());
            var jsonArray = packages.Select(p => JsonSerializer.Serialize(new PackageInfo
            {
                Name = p.Name,
                Version = p.Version,
                Size = p.Size,
                Description = p.Description,
                Url = p.Url,
                Repository = p.Repository
            }, ShellyServiceJsonContext.Default.PackageInfo)).ToArray();

            using var writer = context.CreateReplyWriter("as");
            writer.WriteArray(jsonArray);
            context.Reply(writer.CreateMessage());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get installed packages");
            context.ReplyError("org.shelly.Error.GetPackagesFailed", ex.Message);
        }
    }

    private async ValueTask HandleGetAvailablePackagesAsync(MethodContext context)
    {
        try
        {
            _logger.LogInformation("Getting available packages");
            var packages = await Task.Run(() => _alpmManager.GetAvailablePackages());
            var jsonArray = packages.Select(p => JsonSerializer.Serialize(new PackageInfo
            {
                Name = p.Name,
                Version = p.Version,
                Size = p.Size,
                Description = p.Description,
                Url = p.Url,
                Repository = p.Repository
            }, ShellyServiceJsonContext.Default.PackageInfo)).ToArray();

            using var writer = context.CreateReplyWriter("as");
            writer.WriteArray(jsonArray);
            context.Reply(writer.CreateMessage());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available packages");
            context.ReplyError("org.shelly.Error.GetPackagesFailed", ex.Message);
        }
    }

    private async ValueTask HandleGetPackagesNeedingUpdateAsync(MethodContext context)
    {
        try
        {
            _logger.LogInformation("Getting packages needing update");
            var packages = await Task.Run(() => _alpmManager.GetPackagesNeedingUpdate());
            var jsonArray = packages.Select(p => JsonSerializer.Serialize(new PackageUpdateInfo
            {
                Name = p.Name,
                CurrentVersion = p.CurrentVersion,
                NewVersion = p.NewVersion,
                DownloadSize = p.DownloadSize
            }, ShellyServiceJsonContext.Default.PackageUpdateInfo)).ToArray();

            using var writer = context.CreateReplyWriter("as");
            writer.WriteArray(jsonArray);
            context.Reply(writer.CreateMessage());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get packages needing update");
            context.ReplyError("org.shelly.Error.GetPackagesFailed", ex.Message);
        }
    }

    private async ValueTask HandleInstallPackagesAsync(MethodContext context)
    {
        try
        {
            var reader = context.Request.GetBodyReader();
            var packageNames = reader.ReadArrayOfString();
            var flags = reader.ReadUInt32();

            _logger.LogInformation("Installing packages: {Packages}", string.Join(", ", packageNames));
            await Task.Run(() => _alpmManager.InstallPackages(packageNames.ToList(), (AlpmTransFlag)flags));
            ReplyEmpty(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install packages");
            context.ReplyError("org.shelly.Error.InstallFailed", ex.Message);
        }
    }

    private async ValueTask HandleRemovePackagesAsync(MethodContext context)
    {
        try
        {
            var reader = context.Request.GetBodyReader();
            var packageNames = reader.ReadArrayOfString();
            var flags = reader.ReadUInt32();
            _logger.LogInformation("Removing packages: {Packages}", string.Join(", ", packageNames));

            // Set a long timeout for the removal operation if needed, 
            // though D-Bus method calls usually have a default timeout of 25 seconds.
            await Task.Run(() =>
                _alpmManager.RemovePackages(packageNames.ToList(), (AlpmTransFlag)flags));

            _logger.LogInformation("Packages removed successfully");
            ReplyEmpty(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove packages");
            try
            {
                context.ReplyError("org.shelly.Error.RemoveFailed", ex.Message);
            }
            catch (Exception replyEx)
            {
                _logger.LogError(replyEx, "Failed to send error reply for removal");
            }
        }
    }

    private async ValueTask HandleUpdatePackagesAsync(MethodContext context)
    {
        try
        {
            var reader = context.Request.GetBodyReader();
            var packageNames = reader.ReadArrayOfString();
            var flags = reader.ReadUInt32();

            _logger.LogInformation("Updating packages: {Packages}", string.Join(", ", packageNames));
            await Task.Run(() => _alpmManager.UpdatePackages(packageNames.ToList(), (AlpmTransFlag)flags));
            ReplyEmpty(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update packages");
            context.ReplyError("org.shelly.Error.UpdateFailed", ex.Message);
        }
    }

    private async ValueTask HandleSyncSystemUpdateAsync(MethodContext context)
    {
        try
        {
            var reader = context.Request.GetBodyReader();
            var flags = reader.ReadUInt32();

            _logger.LogInformation("Performing system update");
            await Task.Run(() => _alpmManager.SyncSystemUpdate((AlpmTransFlag)flags));
            ReplyEmpty(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform system update");
            context.ReplyError("org.shelly.Error.SystemUpdateFailed", ex.Message);
        }
    }
}