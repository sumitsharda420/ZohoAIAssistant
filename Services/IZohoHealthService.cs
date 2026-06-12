using ZohoAIAssistant.DTOs;

namespace ZohoAIAssistant.Services;

/// <summary>
/// Verifies connectivity to Zoho CRM using the current OAuth token.
/// </summary>
public interface IZohoHealthService
{
    Task<ZohoHealthCheckDto> CheckZohoConnectivityAsync(CancellationToken cancellationToken = default);
}
