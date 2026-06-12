namespace ZohoAIAssistant.Services;

/// <summary>
/// Retrieves and caches Leads module field API names from Zoho CRM field metadata.
/// </summary>
public interface IZohoFieldMetadataService
{
    /// <summary>
    /// Returns comma-separated field batches (max 50 fields each) for Leads API requests.
    /// </summary>
    Task<IReadOnlyList<string>> GetLeadFieldBatchesAsync(CancellationToken cancellationToken = default);
}
