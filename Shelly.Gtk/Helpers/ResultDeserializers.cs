using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Shelly.Gtk.Services;

namespace Shelly.Gtk.Helpers;

public static class ResultDeserializers
{
    public static List<T> DeserializeCliResult<T>(OperationResult result, JsonTypeInfo<List<T>> context) where T : class
    {
        if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
        {
            return [];
        }

        try
        {
            var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmedLine = StripBom(line.Trim());
                if (!trimmedLine.StartsWith("[") || !trimmedLine.EndsWith("]")) continue;
                var updates = JsonSerializer.Deserialize(trimmedLine,
                    context);
                return updates ?? [];
            }

            var allUpdates = JsonSerializer.Deserialize(StripBom(result.Output.Trim()),
                context);
            return allUpdates ?? [];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to parse updates JSON: {ex.Message}");
            return [];
        }
    }

    private static string StripBom(string input)
    {
        return string.IsNullOrEmpty(input) ? input : input.TrimStart('\uFEFF');
    }
}