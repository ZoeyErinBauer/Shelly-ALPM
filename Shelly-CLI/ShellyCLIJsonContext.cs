using System.Collections.Generic;
using System.Text.Json.Serialization;
using PackageManager.Alpm;

namespace Shelly_CLI;

[JsonSerializable(typeof(List<AlpmPackageUpdateDto>))]
[JsonSerializable(typeof(AlpmPackageUpdateDto))]
[JsonSerializable(typeof(List<AlpmPackageDto>))]
[JsonSerializable(typeof(AlpmPackageDto))]
internal partial class ShellyCLIJsonContext : JsonSerializerContext
{
}
