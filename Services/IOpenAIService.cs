using ZohoAIAssistant.DTOs;

namespace ZohoAIAssistant.Services;

/// <summary>
/// OpenAI-powered lead analysis, email drafting, and summarization.
/// </summary>
public interface IOpenAIService
{
    /// <summary>Fetches a lead from Zoho and returns a full AI analysis as JSON.</summary>
    Task<LeadAnalysisResponse> AnalyzeLeadAsync(
        string leadId,
        LeadAnalysisRequest? request = null,
        CancellationToken cancellationToken = default);

    /// <summary>Generates a professional sales follow-up email for the lead.</summary>
    Task<LeadEmailResponse> GenerateEmailAsync(
        string leadId,
        LeadAnalysisRequest? request = null,
        CancellationToken cancellationToken = default);

    /// <summary>Generates a concise lead summary.</summary>
    Task<LeadSummaryResponse> GenerateSummaryAsync(
        string leadId,
        LeadAnalysisRequest? request = null,
        CancellationToken cancellationToken = default);
}
