using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using ZohoAIAssistant.Configuration;
using ZohoAIAssistant.DTOs;
using ZohoAIAssistant.Models;
using ZohoAIAssistant.Services.Exceptions;

namespace ZohoAIAssistant.Services;

/// <summary>
/// Calls the Zoho CRM v7 Leads module. Automatically attaches a valid OAuth access token
/// to every request via <see cref="IZohoOAuthService"/> and fetches all module fields.
/// </summary>
public class ZohoLeadService : IZohoLeadService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IZohoOAuthService _oauthService;
    private readonly IZohoFieldMetadataService _fieldMetadataService;
    private readonly ZohoSettings _settings;
    private readonly ILogger<ZohoLeadService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ZohoLeadService(
        IHttpClientFactory httpClientFactory,
        IZohoOAuthService oauthService,
        IZohoFieldMetadataService fieldMetadataService,
        IOptions<ZohoSettings> settings,
        ILogger<ZohoLeadService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _oauthService = oauthService;
        _fieldMetadataService = fieldMetadataService;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<ZohoLeadListResponseDto> GetAllLeadsAsync(CancellationToken cancellationToken = default)
        => FetchLeadsWithAllFieldsAsync(perPage: null, cancellationToken);

    /// <inheritdoc />
    public Task<ZohoLeadListResponseDto> GetFirstLeadsAsync(
        int count = 5,
        CancellationToken cancellationToken = default)
    {
        var safeCount = Math.Clamp(count, 1, 200);
        _logger.LogInformation("Fetching first {Count} leads with all fields from Zoho CRM.", safeCount);
        return FetchLeadsWithAllFieldsAsync(perPage: safeCount, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZohoLeadDto?> GetLeadByIdAsync(string leadId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(leadId))
        {
            throw new ArgumentException("Lead ID cannot be empty.", nameof(leadId));
        }

        _logger.LogInformation("Fetching lead {LeadId} with all fields from Zoho CRM.", leadId);

        var fieldBatches = await _fieldMetadataService.GetLeadFieldBatchesAsync(cancellationToken);
        ZohoLeadDto? mergedLead = null;

        foreach (var fields in fieldBatches)
        {
            var url = BuildLeadByIdUrl(leadId, fields);
            var body = await SendAuthenticatedGetForBodyAsync(url, cancellationToken, allowNotFound: true);

            if (body is null)
            {
                return null;
            }

            var zohoResponse = JsonSerializer.Deserialize<ZohoApiResponse<JsonElement>>(body, JsonOptions);
            var record = zohoResponse?.Data?.FirstOrDefault() ?? default;

            if (record.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            mergedLead ??= new ZohoLeadDto();
            mergedLead.MergeFields(ZohoJsonHelper.JsonElementToDictionary(record));
        }

        return mergedLead;
    }

    /// <inheritdoc />
    public async Task<string> GetLeadNotesAsync(string leadId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(leadId))
        {
            throw new ArgumentException("Lead ID cannot be empty.", nameof(leadId));
        }

        var url = QueryHelpers.AddQueryString(
            $"{_settings.ApiBaseUrl.TrimEnd('/')}/crm/v7/Leads/{leadId}/Notes",
            new Dictionary<string, string?>
            {
                ["fields"] = "Note_Content,Note_Title,Created_Time",
                ["per_page"] = "10"
            });

        _logger.LogInformation("Fetching notes for lead {LeadId} from Zoho CRM.", leadId);

        var body = await SendAuthenticatedGetForBodyAsync(url, cancellationToken, allowNotFound: true);
        if (body is null)
        {
            return string.Empty;
        }

        var zohoResponse = JsonSerializer.Deserialize<ZohoApiResponse<JsonElement>>(body, JsonOptions);
        if (zohoResponse?.Data is null || zohoResponse.Data.Count == 0)
        {
            return string.Empty;
        }

        var noteLines = new List<string>();
        foreach (var note in zohoResponse.Data)
        {
            if (note.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var noteFields = ZohoJsonHelper.JsonElementToDictionary(note);
            var title = noteFields.GetValueOrDefault("Note_Title")?.ToString();
            var content = noteFields.GetValueOrDefault("Note_Content")?.ToString();
            var created = noteFields.GetValueOrDefault("Created_Time")?.ToString();

            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            noteLines.Add(string.IsNullOrWhiteSpace(title)
                ? $"- ({created}): {content}"
                : $"- {title} ({created}): {content}");
        }

        return string.Join(Environment.NewLine, noteLines);
    }

    /// <summary>
    /// Fetches leads using every field batch and merges records by id.
    /// Zoho allows max 50 fields per request, so multiple batches are combined.
    /// </summary>
    private async Task<ZohoLeadListResponseDto> FetchLeadsWithAllFieldsAsync(
        int? perPage,
        CancellationToken cancellationToken)
    {
        var fieldBatches = await _fieldMetadataService.GetLeadFieldBatchesAsync(cancellationToken);
        var mergedLeads = new Dictionary<string, ZohoLeadDto>(StringComparer.OrdinalIgnoreCase);

        ZohoPaginationInfo? pagination = null;

        foreach (var fields in fieldBatches)
        {
            var url = BuildLeadsListUrl(fields, perPage);
            var body = await SendAuthenticatedGetForBodyAsync(url, cancellationToken);

            var zohoResponse = JsonSerializer.Deserialize<ZohoApiResponse<JsonElement>>(body, JsonOptions);
            pagination ??= zohoResponse?.Info;

            foreach (var record in zohoResponse?.Data ?? [])
            {
                if (record.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var fieldsMap = ZohoJsonHelper.JsonElementToDictionary(record);
                var id = fieldsMap.GetValueOrDefault("id")?.ToString();

                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                if (!mergedLeads.TryGetValue(id, out var lead))
                {
                    lead = new ZohoLeadDto();
                    mergedLeads[id] = lead;
                }

                lead.MergeFields(fieldsMap);
            }
        }

        var leads = mergedLeads.Values.ToList();
        var totalFieldCount = leads.FirstOrDefault()?.Count ?? 0;

        return new ZohoLeadListResponseDto
        {
            Leads = leads,
            Count = pagination?.Count ?? leads.Count,
            Page = pagination?.Page ?? 1,
            PerPage = pagination?.PerPage ?? leads.Count,
            MoreRecords = pagination?.MoreRecords ?? false,
            FieldBatchCount = fieldBatches.Count,
            TotalFieldCount = totalFieldCount
        };
    }

    private string BuildLeadsListUrl(string fields, int? perPage = null)
    {
        var query = new Dictionary<string, string?> { ["fields"] = fields };

        if (perPage.HasValue)
        {
            query["per_page"] = perPage.Value.ToString();
        }

        return QueryHelpers.AddQueryString(
            $"{_settings.ApiBaseUrl.TrimEnd('/')}/crm/v7/Leads",
            query);
    }

    private string BuildLeadByIdUrl(string leadId, string fields)
    {
        return QueryHelpers.AddQueryString(
            $"{_settings.ApiBaseUrl.TrimEnd('/')}/crm/v7/Leads/{leadId}",
            new Dictionary<string, string?> { ["fields"] = fields });
    }

    private async Task<string> SendAuthenticatedGetForBodyAsync(
        string url,
        CancellationToken cancellationToken,
        bool allowNotFound = false)
    {
        _logger.LogInformation("Calling Zoho CRM: {Url}", url);

        var response = await SendAuthenticatedGetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Zoho CRM returned 404 for {Url}.", url);
            response.Dispose();
            return null!;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Zoho CRM request failed with status {StatusCode}. Response: {Response}",
                response.StatusCode,
                body);
            response.Dispose();
            throw new ZohoApiException(
                $"Zoho CRM request failed: {response.StatusCode}. {body}",
                (int)response.StatusCode);
        }

        response.Dispose();
        return body;
    }

    private async Task<HttpResponseMessage> SendAuthenticatedGetAsync(
        string url,
        CancellationToken cancellationToken,
        bool isRetry = false)
    {
        var accessToken = await _oauthService.GetAccessTokenAsync(cancellationToken);
        var client = _httpClientFactory.CreateClient("ZohoCrm");

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Authorization", $"Zoho-oauthtoken {accessToken}");

        var response = await client.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized && !isRetry)
        {
            _logger.LogWarning("Zoho CRM returned 401; forcing token refresh and retrying.");
            response.Dispose();
            await _oauthService.RefreshAccessTokenAsync(cancellationToken);
            return await SendAuthenticatedGetAsync(url, cancellationToken, isRetry: true);
        }

        return response;
    }
}
