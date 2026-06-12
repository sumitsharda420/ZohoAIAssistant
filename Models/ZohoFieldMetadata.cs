using System.Text.Json.Serialization;

namespace ZohoAIAssistant.Models;

/// <summary>Response from GET /crm/v7/settings/fields?module=Leads.</summary>
public class ZohoFieldsMetadataResponse
{
    [JsonPropertyName("fields")]
    public List<ZohoFieldMetadataItem>? Fields { get; set; }
}

/// <summary>Single field definition in Zoho field metadata.</summary>
public class ZohoFieldMetadataItem
{
    [JsonPropertyName("api_name")]
    public string? ApiName { get; set; }

    [JsonPropertyName("data_type")]
    public string? DataType { get; set; }

    [JsonPropertyName("visible")]
    public bool? Visible { get; set; }
}
