using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PackageManager.Flatpak;

// Simplified and cleaned up properties for Flathub API response
[SuppressMessage("ReSharper", "InconsistentNaming")]
public class Hit
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("keywords")]
    public List<string>? Keywords { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("project_license")]
    public string? ProjectLicense { get; set; }

    [JsonPropertyName("is_free_license")]
    public bool? IsFreeLicense { get; set; }

    [JsonPropertyName("app_id")]
    public string? AppId { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("main_categories")]
    [JsonConverter(typeof(StringOrArrayConverter))]
    public List<string>? MainCategories { get; set; }

    [JsonPropertyName("sub_categories")]
    public List<string>? SubCategories { get; set; }

    [JsonPropertyName("developer_name")]
    public string? DeveloperName { get; set; }

    [JsonPropertyName("verification_verified")]
    public bool? VerificationVerified { get; set; }

    [JsonPropertyName("runtime")]
    public string? Runtime { get; set; }

    [JsonPropertyName("updated_at")]
    public int? UpdatedAt { get; set; }

    [JsonPropertyName("arches")]
    public List<string> Arches { get; set; } = [];

    [JsonPropertyName("added_at")]
    public int AddedAt { get; set; }

    [JsonPropertyName("trending")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public double? Trending { get; set; }

    [JsonPropertyName("installs_last_month")]
    public int? InstallsLastMonth { get; set; }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class FlatpakApiResponse
{
    [JsonPropertyName("hits")]
    public List<Hit>? Hits { get; set; }

    [JsonPropertyName("query")]
    public string? Query { get; set; }

    [JsonPropertyName("processingTimeMs")]
    public int? ProcessingTimeMs { get; set; }

    [JsonPropertyName("hitsPerPage")]
    public int? HitsPerPage { get; set; }

    [JsonPropertyName("page")]
    public int? Page { get; set; }

    [JsonPropertyName("totalPages")]
    public int? TotalPages { get; set; }

    [JsonPropertyName("totalHits")]
    public int? TotalHits { get; set; }
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