using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PackageManager.Flatpak;

//TODO: Cleanup and make properties with an actual name and map to property

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class AdditionalProp1
{
    public string? additionalProp1 { get; set; }
    public string? additionalProp2 { get; set; }
    public string? additionalProp3 { get; set; }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class AdditionalProp2
{
    public string? additionalProp1 { get; set; }
    public string? additionalProp2 { get; set; }
    public string? additionalProp3 { get; set; }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class AdditionalProp3
{
    public string? additionalProp1 { get; set; }
    public string? additionalProp2 { get; set; }
    public string? additionalProp3 { get; set; }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class FacetDistribution
{
    public AdditionalProp1? additionalProp1 { get; set; }
    public AdditionalProp2? additionalProp2 { get; set; }
    public AdditionalProp3? additionalProp3 { get; set; }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class FacetStats
{
    public AdditionalProp1? additionalProp1 { get; set; }
    public AdditionalProp2? additionalProp2 { get; set; }
    public AdditionalProp3? additionalProp3 { get; set; }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class Hit
{
    public string? name { get; set; }
    public List<string>? keywords { get; set; }
    public string? summary { get; set; }
    public string? description { get; set; }
    public string? id { get; set; }
    public string? type { get; set; }
    public Translations? translations { get; set; }
    public string? project_license { get; set; }
    public bool? is_free_license { get; set; }
    public string? app_id { get; set; }
    public string? icon { get; set; }

    [JsonConverter(typeof(StringOrArrayConverter))]
    public List<string>? main_categories { get; set; }

    public List<string>? sub_categories { get; set; }
    public string? developer_name { get; set; }
    public bool? verification_verified { get; set; }
    public string? verification_method { get; set; }
    public string? verification_login_name { get; set; }
    public string? verification_login_provider { get; set; }
    public string? verification_website { get; set; }
    public string? verification_timestamp { get; set; }
    public string? runtime { get; set; }
    public int? updated_at { get; set; }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public List<string> arches { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public int added_at { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public double? trending { get; set; }

    public int? installs_last_month { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public double? favorites_count { get; set; }

    public bool? isMobileFriendly { get; set; }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class FlatpakApiResponse
{
    public List<Hit>? hits { get; set; }
    public string? query { get; set; }
    public int? processingTimeMs { get; set; }

    [JsonPropertyName("hitsPerPage")] public int? hitsPerPage { get; set; }

    [JsonPropertyName("page")] public int? page { get; set; }

    [JsonPropertyName("totalPages")] public int? totalPages { get; set; }

    [JsonPropertyName("totalHits")] public int? totalHits { get; set; }
    public FacetDistribution? facetDistribution { get; set; }
    public FacetStats? facetStats { get; set; }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class Translations
{
    public AdditionalProp1? additionalProp1 { get; set; }
    public AdditionalProp2? additionalProp2 { get; set; }
    public AdditionalProp3? additionalProp3 { get; set; }
}

internal sealed class StringOrArrayConverter : JsonConverter<List<string>?>
{
    public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => null,

            JsonTokenType.String => Wrap(reader.GetString()),

            JsonTokenType.StartArray => ReadArray(ref reader),

            _ => throw new JsonException($"Unexpected token {reader.TokenType} when parsing string-or-array.")
        };

        static List<string> Wrap(string? s) =>
            string.IsNullOrWhiteSpace(s) ? new List<string>() : new List<string> { s! };

        static List<string> ReadArray(ref Utf8JsonReader reader)
        {
            var list = new List<string>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    return list;

                if (reader.TokenType == JsonTokenType.String)
                    list.Add(reader.GetString() ?? string.Empty);
                else if (reader.TokenType == JsonTokenType.Null)
                    list.Add(string.Empty);
                else
                    list.Add(JsonDocument.ParseValue(ref reader).RootElement.ToString());
            }

            throw new JsonException("Unexpected end of JSON while reading array.");
        }
    }

    public override void Write(Utf8JsonWriter writer, List<string>? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        foreach (var s in value)
            writer.WriteStringValue(s);
        writer.WriteEndArray();
    }
}