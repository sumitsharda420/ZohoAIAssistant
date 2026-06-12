using ZohoAIAssistant.Models;

namespace ZohoAIAssistant.DTOs;

/// <summary>
/// A Zoho Lead record with all CRM fields returned as a flat key/value map (Zoho API names).
/// </summary>
public class ZohoLeadDto : Dictionary<string, object?>
{
    public ZohoLeadDto()
    {
    }

    public ZohoLeadDto(IDictionary<string, object?> fields) : base(fields, StringComparer.OrdinalIgnoreCase)
    {
    }

    /// <summary>Merges additional field values into this lead (used when batching field requests).</summary>
    public void MergeFields(IDictionary<string, object?> fields)
    {
        foreach (var field in fields)
        {
            this[field.Key] = field.Value;
        }
    }
}

/// <summary>
/// Paginated list response returned by GET /api/leads.
/// </summary>
public class ZohoLeadListResponseDto
{
    public List<ZohoLeadDto> Leads { get; set; } = [];
    public int Count { get; set; }
    public int Page { get; set; }
    public int PerPage { get; set; }
    public bool MoreRecords { get; set; }
    public int FieldBatchCount { get; set; }
    public int TotalFieldCount { get; set; }
}
