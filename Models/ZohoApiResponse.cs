using System.Text.Json.Serialization;

namespace ZohoAIAssistant.Models;

/// <summary>
/// Generic wrapper for Zoho CRM v7 list/detail API responses.
/// </summary>
/// <typeparam name="T">The record type contained in the data array.</typeparam>
public class ZohoApiResponse<T>
{
    [JsonPropertyName("data")]
    public List<T>? Data { get; set; }

    [JsonPropertyName("info")]
    public ZohoPaginationInfo? Info { get; set; }
}

/// <summary>
/// Pagination metadata returned alongside list endpoints.
/// </summary>
public class ZohoPaginationInfo
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("per_page")]
    public int PerPage { get; set; }

    [JsonPropertyName("more_records")]
    public bool MoreRecords { get; set; }
}

/// <summary>
/// Error payload returned by Zoho when a request fails.
/// </summary>
public class ZohoErrorResponse
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("details")]
    public Dictionary<string, object>? Details { get; set; }
}
