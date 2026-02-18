using System.Collections.Generic;
using System.Text.Json.Serialization;
using PackageManager.Alpm;
using PackageManager.Aur.Models;
using PackageManager.Flatpak;
using Shelly_UI.Models;

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
[JsonSerializable(typeof(List<PackageBuild>))]
internal partial class ShellyUIJsonContext : JsonSerializerContext
{
}