using System.Text.Json;

namespace ZohoAIAssistant.Services;

/// <summary>Converts Zoho CRM JSON payloads into API-friendly dictionaries.</summary>
public static class ZohoJsonHelper
{
    /// <summary>Converts a Zoho record JSON object into a flat dictionary.</summary>
    public static Dictionary<string, object?> JsonElementToDictionary(JsonElement element)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (element.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var property in element.EnumerateObject())
        {
            result[property.Name] = JsonElementToObject(property.Value);
        }

        return result;
    }

    /// <summary>Recursively converts <see cref="JsonElement"/> values to CLR types for JSON serialization.</summary>
    public static object? JsonElementToObject(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var longValue)
                ? longValue
                : element.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(property => property.Name, property => JsonElementToObject(property.Value)),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(JsonElementToObject)
                .ToList(),
            _ => element.GetRawText()
        };
}
