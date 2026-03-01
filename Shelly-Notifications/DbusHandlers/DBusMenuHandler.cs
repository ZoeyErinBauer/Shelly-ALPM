using Shelly_Notifications.Services;
using Tmds.DBus.Protocol;

namespace Shelly_Notifications.DbusHandlers;

internal class DBusMenuHandler(Connection connection) : IPathMethodHandler
{
    public string Path => "/MenuBar";
    public bool HandlesChildPaths => false;

    public event Action? OnExitRequested;

    private static readonly Dictionary<int, (string Label, string Type, bool Enabled, string icon)> Items = new()
    {
        [1] = ("Open Shelly", "standard", true, "shelly"),
        [2] = ("Update Packages", "standard", true, ""),
        [3] = ("Check for Updates", "standard", true, ""),
        [4] = ("", "separator", false, ""),
        [5] = ("Exit", "standard", true, ""),
    };

    private static readonly int[] RootChildren = [1, 2, 3, 4, 5];

    public ValueTask HandleMethodAsync(MethodContext context)
    {
        var req = context.Request;

        if (req.Interface.SequenceEqual("com.canonical.dbusmenu"u8))
        {
            if (req.Member.SequenceEqual("GetLayout"u8))
                return HandleGetLayout(context);
            if (req.Member.SequenceEqual("Event"u8))
                return HandleEvent(context);
            if (req.Member.SequenceEqual("EventGroup"u8))
                return HandleEventGroup(context);
            if (req.Member.SequenceEqual("GetGroupProperties"u8))
                return HandleGetGroupProperties(context);
            if (req.Member.SequenceEqual("GetProperty"u8))
                return HandleGetProperty(context);
            if (req.Member.SequenceEqual("AboutToShow"u8))
            {
                var reader = req.GetBodyReader();
                reader.ReadInt32();
                using var w = context.CreateReplyWriter("b");
                w.WriteBool(false);
                context.Reply(w.CreateMessage());
                return ValueTask.CompletedTask;
            }

            var member = System.Text.Encoding.UTF8.GetString(req.Member);
            Console.WriteLine($"[DBusMenu] Unhandled member: {member}");
            context.ReplyError("org.freedesktop.DBus.Error.UnknownMethod", $"Unknown: {member}");
            return ValueTask.CompletedTask;
        }

        if (req.Interface.SequenceEqual("org.freedesktop.DBus.Properties"u8))
        {
            if (req.Member.SequenceEqual("GetAll"u8))
            {
                using var w = context.CreateReplyWriter("a{sv}");
                w.WriteDictionary(new Dictionary<string, VariantValue>
                {
                    { "Version", (VariantValue)3u },
                    { "TextDirection", (VariantValue)"ltr" },
                    { "Status", (VariantValue)"normal" },
                    { "IconThemePath", (VariantValue)"" },
                });
                context.Reply(w.CreateMessage());
                return ValueTask.CompletedTask;
            }

            if (req.Member.SequenceEqual("Get"u8))
            {
                var reader = req.GetBodyReader();
                _ = reader.ReadString();
                var prop = reader.ReadString();
                using var w = context.CreateReplyWriter("v");
                switch (prop)
                {
                    case "Version": w.WriteVariantUInt32(3u); break;
                    case "TextDirection": w.WriteVariantString("ltr"); break;
                    case "Status": w.WriteVariantString("normal"); break;
                    default: w.WriteVariantString(""); break;
                }

                context.Reply(w.CreateMessage());
                return ValueTask.CompletedTask;
            }
        }

        context.ReplyError("org.freedesktop.DBus.Error.UnknownMethod", "Unknown method");
        return ValueTask.CompletedTask;
    }

    private VariantValue BuildMenuItemVariant(int id, string label, string type, bool enabled, string icon)
    {
        Dict<string, VariantValue> props = new(new Dictionary<string, VariantValue>
        {
            { "label", label },
            { "type", type },
            { "enabled", enabled },
            { "visible", true },
            {"icon-name", icon}
        });

        return VariantValue.Struct(
            VariantValue.Int32(id),
            props,
            VariantValue.ArrayOfVariant(Array.Empty<VariantValue>())
        );
    }

    private ValueTask HandleGetLayout(MethodContext context)
    {
        var reader = context.Request.GetBodyReader();
        reader.ReadInt32();
        reader.ReadInt32();
        var a = reader.ReadArrayStart(DBusType.String);
        while (reader.HasNext(a)) reader.ReadString();

        using var w = context.CreateReplyWriter("u(ia{sv}av)");
        w.WriteUInt32(1);

        w.WriteStructureStart();
        w.WriteInt32(0);

        var rootDict = w.WriteDictionaryStart();
        w.WriteDictionaryEntryStart();
        w.WriteString("children-display");
        w.WriteVariantString("submenu");
        w.WriteDictionaryEnd(rootDict);

        var av = w.WriteArrayStart(DBusType.Variant);
        foreach (var id in RootChildren)
            if (Items.TryGetValue(id, out var item))
                w.WriteVariant(BuildMenuItemVariant(id, item.Label, item.Type, item.Enabled, item.icon));
        w.WriteArrayEnd(av);

        context.Reply(w.CreateMessage());
        return ValueTask.CompletedTask;
    }

    private async ValueTask HandleEvent(MethodContext context)
    {
        var reader = context.Request.GetBodyReader();
        var id = reader.ReadInt32();
        var event_ = reader.ReadString();
        reader.ReadVariantValue();
        reader.ReadUInt32();

        if (event_ == "clicked")
        {
            switch (id)
            {
                case 1: AppRunner.LaunchAppIfNotRunning(""); break;
                case 2: AppRunner.LaunchAppIfNotRunning("--page UpdatePackage");  break;
                case 3: new NotificationHandler().SendNotif(connection, $"Updates available: {await new UpdateService().CheckForUpdates()}"); break;
                case 5: OnExitRequested?.Invoke(); break;
            }
        }

        using var w = context.CreateReplyWriter("");
        context.Reply(w.CreateMessage());
    }

    private static ValueTask HandleEventGroup(MethodContext context)
    {
        using var w = context.CreateReplyWriter("ai");
        var arr = w.WriteArrayStart(DBusType.Int32);
        w.WriteArrayEnd(arr);
        context.Reply(w.CreateMessage());
        return ValueTask.CompletedTask;
    }

    private static ValueTask HandleGetGroupProperties(MethodContext context)
    {
        var reader = context.Request.GetBodyReader();
        var ids = new List<int>();
        var arrStart = reader.ReadArrayStart(DBusType.Int32);
        while (reader.HasNext(arrStart))
            ids.Add(reader.ReadInt32());

        using var w = context.CreateReplyWriter("a(ia{sv})");
        var arr = w.WriteArrayStart(DBusType.Struct);
        foreach (var id in ids)
        {
            w.WriteStructureStart();
            w.WriteInt32(id);
            var props = new Dictionary<string, VariantValue>();
            if (Items.TryGetValue(id, out var item))
            {
                props["label"] = item.Label;
                props["type"] = item.Type;
                props["enabled"] = true;
                props["visible"] = true;
            }

            w.WriteDictionary(props);
            w.WriteArrayEnd(arr);
        }

        w.WriteArrayEnd(arr);

        context.Reply(w.CreateMessage());
        return ValueTask.CompletedTask;
    }

    private static ValueTask HandleGetProperty(MethodContext context)
    {
        var reader = context.Request.GetBodyReader();

        using var w = context.CreateReplyWriter("v");
        switch (reader.ReadString())
        {
            case "label" when Items.TryGetValue(reader.ReadInt32(), out var item):
                w.WriteVariantString(item.Label);
                break;
            case "enabled":
            case "visible":
                w.WriteVariantBool(true);
                break;
            default:
                w.WriteVariantString("");
                break;
        }

        context.Reply(w.CreateMessage());
        return ValueTask.CompletedTask;
    }
}