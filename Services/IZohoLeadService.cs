using ZohoAIAssistant.DTOs;

namespace ZohoAIAssistant.Services;

/// <summary>
/// Provides read access to Zoho CRM Lead records via the v7 REST API.
/// </summary>
public interface IZohoLeadService
{
    /// <summary>Retrieves all leads (first page) from Zoho CRM.</summary>
    Task<ZohoLeadListResponseDto> GetAllLeadsAsync(CancellationToken cancellationToken = default);

    /// <summary>Retrieves the first N leads from Zoho CRM (used by the test endpoint).</summary>
    Task<ZohoLeadListResponseDto> GetFirstLeadsAsync(int count = 5, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a single lead by its Zoho record ID.</summary>
    Task<ZohoLeadDto?> GetLeadByIdAsync(string leadId, CancellationToken cancellationToken = default);

    /// <summary>Retrieves note text linked to a lead from the Zoho Notes related list.</summary>
    Task<string> GetLeadNotesAsync(string leadId, CancellationToken cancellationToken = default);
}
