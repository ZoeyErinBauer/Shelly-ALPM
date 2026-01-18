using System.Text.Json.Serialization;
using Shelly.Protocol;
using Shelly_UI.Models;

namespace Shelly_UI;

[JsonSerializable(typeof(ShellyConfig))]
[JsonSerializable(typeof(CachedRssModel))]
[JsonSerializable(typeof(RssModel))]
[JsonSerializable(typeof(GitHubRelease))]
[JsonSerializable(typeof(GitHubAsset))]
[JsonSerializable(typeof(GitHubAsset[]))]
[JsonSerializable(typeof(PackageInfo))]
[JsonSerializable(typeof(PackageUpdateInfo))]
internal partial class ShellyUIJsonContext : JsonSerializerContext
{
}
