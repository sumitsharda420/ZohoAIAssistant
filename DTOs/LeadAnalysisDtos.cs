using System.Text.Json.Serialization;

namespace ZohoAIAssistant.DTOs;

/// <summary>Optional input for AI lead analysis endpoints.</summary>
public class LeadAnalysisRequest
{
    /// <summary>Extra instructions or context appended to the AI prompt.</summary>
    public string? AdditionalContext { get; set; }
}

/// <summary>Structured AI analysis returned by POST /api/ai/analyze-lead/{leadId}.</summary>
public class LeadAnalysisResponse
{
    public string LeadId { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string LeadSummary { get; set; } = string.Empty;
    public string RecommendedNextAction { get; set; } = string.Empty;
    public string FollowUpEmailDraft { get; set; } = string.Empty;
    public int LeadQualityScore { get; set; }
    public string Model { get; set; } = string.Empty;
}

/// <summary>Response from POST /api/ai/generate-email/{leadId}.</summary>
public class LeadEmailResponse
{
    public string LeadId { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public string EmailDraft { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}

/// <summary>Response from POST /api/ai/generate-summary/{leadId}.</summary>
public class LeadSummaryResponse
{
    public string LeadId { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}

/// <summary>CRM fields extracted from a Zoho lead before sending to OpenAI.</summary>
public class LeadContextDto
{
    public string LeadId { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

/// <summary>JSON shape expected from OpenAI for full lead analysis.</summary>
internal class OpenAIAnalysisResult
{
    [JsonPropertyName("leadSummary")]
    public string? LeadSummary { get; set; }

    [JsonPropertyName("recommendedNextAction")]
    public string? RecommendedNextAction { get; set; }

    [JsonPropertyName("followUpEmailDraft")]
    public string? FollowUpEmailDraft { get; set; }

    [JsonPropertyName("leadQualityScore")]
    public int LeadQualityScore { get; set; }
}

/// <summary>JSON shape expected from OpenAI for email generation.</summary>
internal class OpenAIEmailResult
{
    [JsonPropertyName("emailDraft")]
    public string? EmailDraft { get; set; }
}

/// <summary>JSON shape expected from OpenAI for summary generation.</summary>
internal class OpenAISummaryResult
{
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }
}
