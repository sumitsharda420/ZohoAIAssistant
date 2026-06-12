using Microsoft.AspNetCore.Mvc;
using ZohoAIAssistant.DTOs;
using ZohoAIAssistant.Services;
using ZohoAIAssistant.Services.Exceptions;

namespace ZohoAIAssistant.Controllers;

/// <summary>
/// AI-powered lead analysis, email drafting, and summarization via OpenAI.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AIController : ControllerBase
{
    private readonly IOpenAIService _openAIService;
    private readonly ILogger<AIController> _logger;

    public AIController(IOpenAIService openAIService, ILogger<AIController> logger)
    {
        _openAIService = openAIService;
        _logger = logger;
    }

    /// <summary>
    /// Analyzes a Zoho CRM lead using OpenAI and returns structured JSON insights.
    /// </summary>
    /// <remarks>
    /// **Workflow**
    ///
    /// 1. Fetches the lead from Zoho CRM (all fields + related Notes).
    /// 2. Extracts Lead Name, Company, Email, Phone, Description, and Notes.
    /// 3. Sends the context to OpenAI with a structured analysis prompt.
    /// 4. Returns JSON containing summary, next action, email draft, and quality score.
    ///
    /// **Example response**
    /// ```json
    /// {
    ///   "leadId": "123456789",
    ///   "leadName": "Jane Doe",
    ///   "company": "Acme Corp",
    ///   "leadSummary": "...",
    ///   "recommendedNextAction": "...",
    ///   "followUpEmailDraft": "...",
    ///   "leadQualityScore": 82,
    ///   "model": "gpt-4o-mini"
    /// }
    /// ```
    ///
    /// Configure `OpenAI:ApiKey` and `OpenAI:Model` in appsettings.json before calling.
    /// </remarks>
    /// <param name="leadId">Zoho CRM Lead record ID.</param>
    /// <param name="request">Optional additional context for the AI prompt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Lead analysis generated successfully.</response>
    /// <response code="404">Lead not found in Zoho CRM.</response>
    /// <response code="500">OpenAI API key missing from configuration.</response>
    /// <response code="502">OpenAI or Zoho API returned an error.</response>
    [HttpPost("analyze-lead/{leadId}")]
    [ProducesResponseType(typeof(LeadAnalysisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<LeadAnalysisResponse>> AnalyzeLead(
        string leadId,
        [FromBody] LeadAnalysisRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("AI analyze-lead requested for lead {LeadId}.", leadId);
            var result = await _openAIService.AnalyzeLeadAsync(leadId, request, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Lead Not Found",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
        catch (OpenAIApiException ex)
        {
            _logger.LogError(ex, "OpenAI analyze-lead failed for lead {LeadId}.", leadId);
            return StatusCode(ex.StatusCode, new ProblemDetails
            {
                Title = "OpenAI Error",
                Detail = ex.Message,
                Status = ex.StatusCode
            });
        }
        catch (ZohoApiException ex)
        {
            _logger.LogError(ex, "Zoho error during analyze-lead for lead {LeadId}.", leadId);
            return StatusCode(ex.StatusCode, new ProblemDetails
            {
                Title = "Zoho CRM Error",
                Detail = ex.Message,
                Status = ex.StatusCode
            });
        }
    }

    /// <summary>
    /// Generates a professional sales follow-up email for a Zoho CRM lead.
    /// </summary>
    /// <remarks>
    /// Fetches lead context from Zoho CRM and returns only the email draft.
    ///
    /// **Example response**
    /// ```json
    /// {
    ///   "leadId": "123456789",
    ///   "leadName": "Jane Doe",
    ///   "emailDraft": "Subject: Following up on our conversation...",
    ///   "model": "gpt-4o-mini"
    /// }
    /// ```
    /// </remarks>
    /// <param name="leadId">Zoho CRM Lead record ID.</param>
    /// <param name="request">Optional additional context for the AI prompt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Email draft generated successfully.</response>
    /// <response code="404">Lead not found in Zoho CRM.</response>
    /// <response code="502">OpenAI or Zoho API returned an error.</response>
    [HttpPost("generate-email/{leadId}")]
    [ProducesResponseType(typeof(LeadEmailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<LeadEmailResponse>> GenerateEmail(
        string leadId,
        [FromBody] LeadAnalysisRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("AI generate-email requested for lead {LeadId}.", leadId);
            var result = await _openAIService.GenerateEmailAsync(leadId, request, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Lead Not Found",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
        catch (OpenAIApiException ex)
        {
            _logger.LogError(ex, "OpenAI generate-email failed for lead {LeadId}.", leadId);
            return StatusCode(ex.StatusCode, new ProblemDetails
            {
                Title = "OpenAI Error",
                Detail = ex.Message,
                Status = ex.StatusCode
            });
        }
        catch (ZohoApiException ex)
        {
            _logger.LogError(ex, "Zoho error during generate-email for lead {LeadId}.", leadId);
            return StatusCode(ex.StatusCode, new ProblemDetails
            {
                Title = "Zoho CRM Error",
                Detail = ex.Message,
                Status = ex.StatusCode
            });
        }
    }

    /// <summary>
    /// Generates a concise AI summary for a Zoho CRM lead.
    /// </summary>
    /// <remarks>
    /// Fetches lead context from Zoho CRM and returns only the summary text.
    ///
    /// **Example response**
    /// ```json
    /// {
    ///   "leadId": "123456789",
    ///   "leadName": "Jane Doe",
    ///   "summary": "High-intent inbound lead from Acme Corp...",
    ///   "model": "gpt-4o-mini"
    /// }
    /// ```
    /// </remarks>
    /// <param name="leadId">Zoho CRM Lead record ID.</param>
    /// <param name="request">Optional additional context for the AI prompt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Summary generated successfully.</response>
    /// <response code="404">Lead not found in Zoho CRM.</response>
    /// <response code="502">OpenAI or Zoho API returned an error.</response>
    [HttpPost("generate-summary/{leadId}")]
    [ProducesResponseType(typeof(LeadSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<LeadSummaryResponse>> GenerateSummary(
        string leadId,
        [FromBody] LeadAnalysisRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("AI generate-summary requested for lead {LeadId}.", leadId);
            var result = await _openAIService.GenerateSummaryAsync(leadId, request, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Lead Not Found",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
        catch (OpenAIApiException ex)
        {
            _logger.LogError(ex, "OpenAI generate-summary failed for lead {LeadId}.", leadId);
            return StatusCode(ex.StatusCode, new ProblemDetails
            {
                Title = "OpenAI Error",
                Detail = ex.Message,
                Status = ex.StatusCode
            });
        }
        catch (ZohoApiException ex)
        {
            _logger.LogError(ex, "Zoho error during generate-summary for lead {LeadId}.", leadId);
            return StatusCode(ex.StatusCode, new ProblemDetails
            {
                Title = "Zoho CRM Error",
                Detail = ex.Message,
                Status = ex.StatusCode
            });
        }
    }
}
