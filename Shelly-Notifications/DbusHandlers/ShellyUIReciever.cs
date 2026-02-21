using Shelly_Notifications.Constants;
using Tmds.DBus.Protocol;

namespace Shelly_Notifications.DbusHandlers;

class ShellyUiReceiver(Action? onRefreshRequested = null) : IPathMethodHandler
{
    public string Path => ShellyConstants.Path;
    public bool HandlesChildPaths => false; 

    public ValueTask HandleMethodAsync(MethodContext context)
    {
        var request = context.Request;

        if (request.InterfaceAsString == ShellyConstants.Interface)
        {
            switch (request.MemberAsString)
            {
                case "RefreshSettings":
                    HandleSettingsRefresh(context);
                    return default;
                case "CloseTray":
                    HandleCloseTray(context);
                    return default;
            }
        }

        context.ReplyError("org.freedesktop.DBus.Error.UnknownMethod",
            $"Method {request.MemberAsString} not found");
        return default;
    }

    private void HandleSettingsRefresh(MethodContext context)
    {
        Console.WriteLine($"RefreshSettings called");

        onRefreshRequested?.Invoke();

        using var writer = context.CreateReplyWriter(null);
        context.Reply(writer.CreateMessage());
    }
    
    private void HandleCloseTray(MethodContext context)
    {
        Console.WriteLine("CloseTray called");

        using var writer = context.CreateReplyWriter(null);
        context.Reply(writer.CreateMessage());

        Environment.Exit(0);
    }
}

