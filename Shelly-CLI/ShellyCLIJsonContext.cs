using System.Text.Json.Serialization;
using PackageManager.AppImage;
using PackageManager.Alpm;
using PackageManager.Alpm.Pacfile;
using PackageManager.Aur.Models;
using PackageManager.Flatpak;
using Shelly_CLI.Commands.Aur;
using Shelly_CLI.Commands.Standard;
using Shelly_CLI.Configuration;

namespace Shelly_CLI;

[JsonSerializable(typeof(List<AlpmPackageUpdateDto>))]
[JsonSerializable(typeof(AlpmPackageUpdateDto))]
[JsonSerializable(typeof(List<AlpmPackageDto>))]
[JsonSerializable(typeof(AlpmPackageDto))]
[JsonSerializable(typeof(List<AurPackageDto>))]
[JsonSerializable(typeof(AurPackageDto))]
[JsonSerializable(typeof(List<AurUpdateDto>))]
[JsonSerializable(typeof(AurUpdateDto))]
[JsonSerializable(typeof(SyncModel))]
[JsonSerializable(typeof(SyncPackageModel))]
[JsonSerializable(typeof(SyncAurModel))]
[JsonSerializable(typeof(SyncFlatpakModel))]
[JsonSerializable(typeof(AurSearchPackageBuild.PackageBuild))]
[JsonSerializable(typeof(List<AurSearchPackageBuild.PackageBuild>))]
[JsonSerializable(typeof(ArchNews.RssModel))]
[JsonSerializable(typeof(List<ArchNews.RssModel>))]
[JsonSerializable(typeof(List<AppImageDto>))]
[JsonSerializable(typeof(AppImageDto))]
[JsonSerializable(typeof(List<AppImageUpdateDto>))]
[JsonSerializable(typeof(AppImageUpdateDto))]
[JsonSerializable(typeof(ShellyConfig))]
[JsonSerializable(typeof(List<FlatpakPackageDto>))]
[JsonSerializable(typeof(FlatpakPackageDto))]
[JsonSerializable(typeof(List<FlatpakRemoteDto>))]
[JsonSerializable(typeof(FlatpakRemoteDto))]
[JsonSerializable(typeof(List<PacfileRecord>))]
[JsonSerializable(typeof(PacfileRecord))]
internal partial class ShellyCLIJsonContext : JsonSerializerContext
{
}