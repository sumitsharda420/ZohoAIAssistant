using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ZohoAIAssistant.Configuration;
using ZohoAIAssistant.DTOs;
using ZohoAIAssistant.Services.Exceptions;

namespace ZohoAIAssistant.Services;

/// <summary>
/// Calls the OpenAI Chat Completions API via <see cref="IHttpClientFactory"/>
/// after loading lead context from Zoho CRM.
/// </summary>
public class OpenAIService : IOpenAIService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IZohoLeadService _leadService;
    private readonly OpenAISettings _settings;
    private readonly ILogger<OpenAIService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OpenAIService(
        IHttpClientFactory httpClientFactory,
        IZohoLeadService leadService,
        IOptions<OpenAISettings> settings,
        ILogger<OpenAIService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _leadService = leadService;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LeadAnalysisResponse> AnalyzeLeadAsync(
        string leadId,
        LeadAnalysisRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();
        var context = await BuildLeadContextAsync(leadId, cancellationToken);

        _logger.LogInformation("Analyzing lead {LeadId} with OpenAI model {Model}.", leadId, _settings.Model);

        var systemPrompt =
            """
            You are a senior B2B sales analyst. Analyze CRM lead data and respond ONLY with valid JSON.
            Use this exact schema:
            {
              "leadSummary": "string",
              "recommendedNextAction": "string",
              "followUpEmailDraft": "string",
              "leadQualityScore": 0
            }
            leadQualityScore must be an integer from 1 to 100.
            """;

        var userPrompt = BuildUserPrompt(
            context,
            request?.AdditionalContext,
            """
            Analyze this CRM lead and provide:
            1. Lead Summary
            2. Recommended Next Action
            3. Follow-up Email Draft
            4. Lead Quality Score (1-100)
            """);

        var analysis = await SendChatCompletionAsync<OpenAIAnalysisResult>(
            systemPrompt,
            userPrompt,
            jsonMode: true,
            cancellationToken);

        return new LeadAnalysisResponse
        {
            LeadId = context.LeadId,
            LeadName = context.LeadName,
            Company = context.Company,
            Email = context.Email,
            Phone = context.Phone,
            Description = context.Description,
            Notes = context.Notes,
            LeadSummary = analysis.LeadSummary ?? string.Empty,
            RecommendedNextAction = analysis.RecommendedNextAction ?? string.Empty,
            FollowUpEmailDraft = analysis.FollowUpEmailDraft ?? string.Empty,
            LeadQualityScore = Math.Clamp(analysis.LeadQualityScore, 1, 100),
            Model = _settings.Model
        };
    }

    /// <inheritdoc />
    public async Task<LeadEmailResponse> GenerateEmailAsync(
        string leadId,
        LeadAnalysisRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();
        var context = await BuildLeadContextAsync(leadId, cancellationToken);

        _logger.LogInformation("Generating follow-up email for lead {LeadId}.", leadId);

        var systemPrompt =
            """
            You are a professional B2B sales copywriter. Write concise, polite follow-up emails.
            Respond ONLY with valid JSON using this schema:
            { "emailDraft": "string" }
            """;

        var userPrompt = BuildUserPrompt(
            context,
            request?.AdditionalContext,
            "Generate a professional sales follow-up email for this CRM lead. Include subject line and body.");

        var result = await SendChatCompletionAsync<OpenAIEmailResult>(
            systemPrompt,
            userPrompt,
            jsonMode: true,
            cancellationToken);

        return new LeadEmailResponse
        {
            LeadId = context.LeadId,
            LeadName = context.LeadName,
            EmailDraft = result.EmailDraft ?? string.Empty,
            Model = _settings.Model
        };
    }

    /// <inheritdoc />
    public async Task<LeadSummaryResponse> GenerateSummaryAsync(
        string leadId,
        LeadAnalysisRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();
        var context = await BuildLeadContextAsync(leadId, cancellationToken);

        _logger.LogInformation("Generating summary for lead {LeadId}.", leadId);

        var systemPrompt =
            """
            You are a CRM assistant. Summarize leads clearly for sales reps.
            Respond ONLY with valid JSON using this schema:
            { "summary": "string" }
            """;

        var userPrompt = BuildUserPrompt(
            context,
            request?.AdditionalContext,
            "Generate a concise lead summary highlighting intent, fit, and any risks.");

        var result = await SendChatCompletionAsync<OpenAISummaryResult>(
            systemPrompt,
            userPrompt,
            jsonMode: true,
            cancellationToken);

        return new LeadSummaryResponse
        {
            LeadId = context.LeadId,
            LeadName = context.LeadName,
            Summary = result.Summary ?? string.Empty,
            Model = _settings.Model
        };
    }

    private async Task<LeadContextDto> BuildLeadContextAsync(string leadId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(leadId))
        {
            throw new ArgumentException("Lead ID cannot be empty.", nameof(leadId));
        }

        var lead = await _leadService.GetLeadByIdAsync(leadId, cancellationToken);
        if (lead is null)
        {
            throw new KeyNotFoundException($"Lead '{leadId}' was not found in Zoho CRM.");
        }

        var notes = await _leadService.GetLeadNotesAsync(leadId, cancellationToken);
        return LeadContextExtractor.Extract(leadId, lead, notes);
    }

    private static string BuildUserPrompt(LeadContextDto context, string? additionalContext, string task)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine(task);
        prompt.AppendLine();
        prompt.AppendLine("Lead data:");
        prompt.AppendLine(LeadContextExtractor.FormatForPrompt(context));

        if (!string.IsNullOrWhiteSpace(additionalContext))
        {
            prompt.AppendLine();
            prompt.AppendLine("Additional context:");
            prompt.AppendLine(additionalContext);
        }

        return prompt.ToString();
    }

    private async Task<T> SendChatCompletionAsync<T>(
        string systemPrompt,
        string userPrompt,
        bool jsonMode,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("OpenAI");

        var requestBody = new
        {
            model = _settings.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            response_format = jsonMode ? new { type = "json_object" } : null,
            temperature = 0.4
        };

        var json = JsonSerializer.Serialize(requestBody, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "OpenAI API call failed with status {StatusCode}. Response: {Response}",
                response.StatusCode,
                responseBody);

            throw new OpenAIApiException(
                $"OpenAI API request failed: {response.StatusCode}. {responseBody}",
                (int)response.StatusCode);
        }

        var completion = JsonSerializer.Deserialize<OpenAIChatCompletionResponse>(responseBody, JsonOptions);
        var messageContent = completion?.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(messageContent))
        {
            throw new OpenAIApiException("OpenAI returned an empty completion.", (int)HttpStatusCode.BadGateway);
        }

        var result = JsonSerializer.Deserialize<T>(messageContent, JsonOptions);
        if (result is null)
        {
            throw new OpenAIApiException(
                $"Failed to parse OpenAI JSON response: {messageContent}",
                (int)HttpStatusCode.BadGateway);
        }

        return result;
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new OpenAIApiException(
                "OpenAI API key is not configured. Set OpenAI:ApiKey in appsettings.json or user secrets.",
                StatusCodes.Status500InternalServerError);
        }
    }

    private sealed class OpenAIChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenAIChoice>? Choices { get; set; }
    }

    private sealed class OpenAIChoice
    {
        [JsonPropertyName("message")]
        public OpenAIMessage? Message { get; set; }
    }

    private sealed class OpenAIMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
