using System.Text.Json.Serialization;
using Shelly.Protocol;

namespace Shelly.Service;

[JsonSerializable(typeof(PackageInfo))]
[JsonSerializable(typeof(PackageUpdateInfo))]
internal partial class ShellyServiceJsonContext : JsonSerializerContext
{
}
