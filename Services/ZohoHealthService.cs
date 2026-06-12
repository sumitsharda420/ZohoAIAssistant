using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using ZohoAIAssistant.Configuration;
using ZohoAIAssistant.DTOs;
using ZohoAIAssistant.Services.Exceptions;

namespace ZohoAIAssistant.Services;

/// <summary>
/// Performs a live health check against Zoho CRM: obtains an access token (refreshing if needed)
/// and calls the Leads API with per_page=1.
/// </summary>
public class ZohoHealthService : IZohoHealthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IZohoOAuthService _oauthService;
    private readonly IZohoFieldMetadataService _fieldMetadataService;
    private readonly ZohoSettings _settings;
    private readonly ILogger<ZohoHealthService> _logger;

    public ZohoHealthService(
        IHttpClientFactory httpClientFactory,
        IZohoOAuthService oauthService,
        IZohoFieldMetadataService fieldMetadataService,
        IOptions<ZohoSettings> settings,
        ILogger<ZohoHealthService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _oauthService = oauthService;
        _fieldMetadataService = fieldMetadataService;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ZohoHealthCheckDto> CheckZohoConnectivityAsync(CancellationToken cancellationToken = default)
    {
        var result = new ZohoHealthCheckDto();

        try
        {
            _logger.LogInformation("Starting Zoho CRM health check.");

            var accessToken = await _oauthService.GetAccessTokenAsync(cancellationToken);
            result.AccessTokenGenerated = !string.IsNullOrWhiteSpace(accessToken);

            var fieldBatches = await _fieldMetadataService.GetLeadFieldBatchesAsync(cancellationToken);
            var fields = fieldBatches[0];

            var url = QueryHelpers.AddQueryString(
                $"{_settings.ApiBaseUrl.TrimEnd('/')}/crm/v7/Leads",
                new Dictionary<string, string?>
                {
                    ["fields"] = fields,
                    ["per_page"] = "1"
                });
            var client = _httpClientFactory.CreateClient("ZohoCrm");

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Zoho-oauthtoken {accessToken}");

            using var response = await client.SendAsync(request, cancellationToken);
            result.HttpStatusCode = (int)response.StatusCode;
            result.CrmApiReachable = response.IsSuccessStatusCode;

            if (response.IsSuccessStatusCode)
            {
                result.Success = true;
                result.Message = "Zoho OAuth and CRM API are working correctly.";
                _logger.LogInformation("Zoho health check succeeded with HTTP {StatusCode}.", response.StatusCode);
            }
            else if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Zoho health check received 401; attempting forced token refresh.");

                await _oauthService.RefreshAccessTokenAsync(cancellationToken);
                var retryToken = await _oauthService.GetAccessTokenAsync(cancellationToken);

                using var retryRequest = new HttpRequestMessage(HttpMethod.Get, url);
                retryRequest.Headers.Add("Authorization", $"Zoho-oauthtoken {retryToken}");

                using var retryResponse = await client.SendAsync(retryRequest, cancellationToken);
                result.HttpStatusCode = (int)retryResponse.StatusCode;
                result.CrmApiReachable = retryResponse.IsSuccessStatusCode;
                result.Success = retryResponse.IsSuccessStatusCode;
                result.Message = retryResponse.IsSuccessStatusCode
                    ? "Zoho CRM reachable after automatic token refresh."
                    : $"Zoho CRM returned {(int)retryResponse.StatusCode} after token refresh.";
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                result.Success = false;
                result.Message = $"Zoho CRM returned HTTP {(int)response.StatusCode}: {body}";
                _logger.LogError("Zoho health check failed. Response: {Response}", body);
            }
        }
        catch (ZohoApiException ex)
        {
            result.Success = false;
            result.Message = ex.Message;
            result.HttpStatusCode = ex.StatusCode;
            _logger.LogError(ex, "Zoho health check failed due to OAuth error.");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Unexpected error during health check: {ex.Message}";
            _logger.LogError(ex, "Zoho health check failed with unexpected error.");
        }

        return result;
    }
}
