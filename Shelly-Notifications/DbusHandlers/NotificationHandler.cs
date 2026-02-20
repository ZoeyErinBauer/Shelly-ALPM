using Tmds.DBus.Protocol;
using Tmds.DBus.SourceGenerator;

namespace Shelly_Notifications.TrayService;

public class NotificationHandler
{
    public async Task SendNotif(Connection connection, string body)
    {
        var notificationProxy = new OrgFreedesktopNotificationsProxy(connection, "org.freedesktop.Notifications",
            "/org/freedesktop/Notifications");

        await notificationProxy.NotifyAsync(
            "Shelly",
            0u,
            "shelly",
            "Shelly Notifications",
            body,
            Array.Empty<string>(),
            new Dictionary<string, VariantValue>(),
            5000
        );
    }
}