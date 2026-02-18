using System.Text.Json.Serialization;
using PackageManager.Alpm;
using PackageManager.Aur.Models;
using Shelly_CLI.Commands.Aur;

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
internal partial class ShellyCLIJsonContext : JsonSerializerContext
{
}
