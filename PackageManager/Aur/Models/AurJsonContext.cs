using System.Text.Json.Serialization;

namespace PackageManager.Aur.Models;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified)]
[JsonSerializable(typeof(AurResponse<AurPackageDto>))]
[JsonSerializable(typeof(AurUpdateDto))]
public partial class AurJsonContext : JsonSerializerContext
{
}