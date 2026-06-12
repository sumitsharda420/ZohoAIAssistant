using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ZohoAIAssistant.Configuration;
using ZohoAIAssistant.Models;
using ZohoAIAssistant.Services.Exceptions;

namespace ZohoAIAssistant.Services;

/// <summary>
/// Calls GET /crm/v7/settings/fields?module=Leads to discover all field API names,
/// then caches the computed field batches for 24 hours.
/// </summary>
public class ZohoFieldMetadataService : IZohoFieldMetadataService
{
    private const string CacheKey = "zoho:leads:field-batches";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IZohoOAuthService _oauthService;
    private readonly IMemoryCache _cache;
    private readonly ZohoSettings _settings;
    private readonly ILogger<ZohoFieldMetadataService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Fallback when metadata is unavailable (requires only Leads module scope).
    private static readonly IReadOnlyList<string> FallbackFieldBatch =
    [
        "id,Owner,Company,First_Name,Last_Name,Full_Name,Email,Secondary_Email,Phone,Mobile,Fax," +
        "Website,Lead_Source,Lead_Status,Industry,Rating,Annual_Revenue,No_of_Employees," +
        "Street,City,State,Zip_Code,Country,Description,Skype_ID,Twitter,Salutation,Designation," +
        "Created_By,Modified_By,Created_Time,Modified_Time,Last_Activity_Time,Email_Opt_Out,Tag," +
        "$converted,$approved,$editable,$currency_symbol,$process_flow,$followed"
    ];

    public ZohoFieldMetadataService(
        IHttpClientFactory httpClientFactory,
        IZohoOAuthService oauthService,
        IMemoryCache cache,
        IOptions<ZohoSettings> settings,
        ILogger<ZohoFieldMetadataService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _oauthService = oauthService;
        _cache = cache;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetLeadFieldBatchesAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<string>? cached) && cached is { Count: > 0 })
        {
            return cached;
        }

        try
        {
            var apiNames = await FetchLeadFieldApiNamesAsync(cancellationToken);
            var batches = ZohoFieldQueryHelper.BuildFieldBatches(apiNames);

            _logger.LogInformation(
                "Loaded {FieldCount} Leads field API names from Zoho metadata ({BatchCount} batch(es)).",
                apiNames.Count,
                batches.Count);

            _cache.Set(CacheKey, batches, TimeSpan.FromHours(24));
            return batches;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to load Leads field metadata. Using fallback field list. " +
                "Re-authorize with ZohoCRM.settings.fields.READ scope for all custom fields.");

            return FallbackFieldBatch;
        }
    }

    private async Task<IReadOnlyList<string>> FetchLeadFieldApiNamesAsync(CancellationToken cancellationToken)
    {
        var url = QueryHelpers.AddQueryString(
            $"{_settings.ApiBaseUrl.TrimEnd('/')}/crm/v7/settings/fields",
            new Dictionary<string, string?>
            {
                ["module"] = "Leads",
                ["type"] = "all"
            });

        var accessToken = await _oauthService.GetAccessTokenAsync(cancellationToken);
        var client = _httpClientFactory.CreateClient("ZohoCrm");

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Authorization", $"Zoho-oauthtoken {accessToken}");

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new ZohoApiException(
                $"Failed to retrieve Leads field metadata: {response.StatusCode}. {body}",
                (int)response.StatusCode);
        }

        var metadata = JsonSerializer.Deserialize<ZohoFieldsMetadataResponse>(body, JsonOptions);

        var apiNames = metadata?.Fields?
            .Where(field => !string.IsNullOrWhiteSpace(field.ApiName))
            .Where(field => field.Visible != false)
            .Select(field => field.ApiName!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        if (apiNames.Count == 0)
        {
            throw new ZohoApiException(
                "Zoho field metadata returned no Leads fields.",
                (int)HttpStatusCode.BadGateway);
        }

        return apiNames;
    }
}
