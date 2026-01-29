using System.Collections.Generic;
using System.Text.Json.Serialization;
using Shelly_UI.Models;

namespace Shelly_UI;

[JsonSerializable(typeof(ShellyConfig))]
[JsonSerializable(typeof(CachedRssModel))]
[JsonSerializable(typeof(RssModel))]
[JsonSerializable(typeof(GitHubRelease))]
[JsonSerializable(typeof(GitHubAsset))]
[JsonSerializable(typeof(GitHubAsset[]))]
[JsonSerializable(typeof(FlatpakModel))]
[JsonSerializable(typeof(List<FlatpakModel>))]
internal partial class ShellyUIJsonContext : JsonSerializerContext
{
}
