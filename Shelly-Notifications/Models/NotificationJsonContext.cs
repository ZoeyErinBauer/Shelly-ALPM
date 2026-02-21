using System.Text.Json.Serialization;

namespace Shelly_Notifications.Models;
[JsonSerializable(typeof(ShellyConfig))]
[JsonSerializable(typeof(SyncModel))]
[JsonSerializable(typeof(SyncPackageModel))]
[JsonSerializable(typeof(SyncAurModel))]
[JsonSerializable(typeof(SyncFlatpakModel))]
internal partial class NotificationJsonContext : JsonSerializerContext
{
}
