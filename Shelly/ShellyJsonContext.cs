using System.Text.Json.Serialization;
using Shelly.Configurations;
using Shelly.Models;

namespace Shelly;

[JsonSerializable(typeof(SyncModel))]
[JsonSerializable(typeof(SyncPackageModel))]
[JsonSerializable(typeof(SyncAurModel))]
[JsonSerializable(typeof(SyncFlatpakModel))]
[JsonSerializable(typeof(ShellyConfig))]
internal partial class ShellyJsonContext : JsonSerializerContext
{
    
}