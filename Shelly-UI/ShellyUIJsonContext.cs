using System.Collections.Generic;
using System.Text.Json.Serialization;
using Shelly_UI.Models;
using Shelly_UI.Models.PackageManagerObjects;

namespace Shelly_UI;


[JsonSerializable(typeof(ShellyConfig))]
[JsonSerializable(typeof(CachedRssModel))]
[JsonSerializable(typeof(RssModel))]
[JsonSerializable(typeof(List<RssModel>))]
[JsonSerializable(typeof(GitHubRelease))]
[JsonSerializable(typeof(GitHubAsset))]
[JsonSerializable(typeof(GitHubAsset[]))]
[JsonSerializable(typeof(List<AlpmPackageUpdateDto>))]
[JsonSerializable(typeof(AlpmPackageUpdateDto))]
[JsonSerializable(typeof(List<AlpmPackageDto>))]
[JsonSerializable(typeof(AlpmPackageDto))]
[JsonSerializable(typeof(List<AurPackageDto>))]
[JsonSerializable(typeof(AurPackageDto))]
[JsonSerializable(typeof(List<AurUpdateDto>))]
[JsonSerializable(typeof(AurUpdateDto))]
[JsonSerializable(typeof(FlatpakModel))]
[JsonSerializable(typeof(List<FlatpakModel>))]
[JsonSerializable(typeof(SyncModel))]
[JsonSerializable(typeof(SyncPackageModel))]
[JsonSerializable(typeof(SyncAurModel))]
[JsonSerializable(typeof(SyncFlatpakModel))]
[JsonSerializable(typeof(FlatpakPackageDto))]
[JsonSerializable(typeof(List<FlatpakPackageDto>))]
internal partial class ShellyUIJsonContext : JsonSerializerContext
{
}