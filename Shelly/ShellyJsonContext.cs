using System.Text.Json.Serialization;
using Shelly.Models;

namespace Shelly;

[JsonSerializable(typeof(SyncModel))]
[JsonSerializable(typeof(SyncPackageModel))]
[JsonSerializable(typeof(SyncAurModel))]
[JsonSerializable(typeof(SyncFlatpakModel))]
internal partial class ShellyJsonContext : JsonSerializerContext
{
    
}