using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PackageManager.Aur.Models;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified)]
[JsonSerializable(typeof(AurResponse<AurPackageDto>))]
[JsonSerializable(typeof(AurUpdateDto))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<AurPackageDto>))]
[JsonSerializable(typeof(Dictionary<string, List<VcsSourceEntry>>))]
public partial class AurJsonContext : JsonSerializerContext
{
}