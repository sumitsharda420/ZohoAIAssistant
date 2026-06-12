using Microsoft.AspNetCore.Mvc;
using ZohoAIAssistant.DTOs;
using ZohoAIAssistant.Services;
using ZohoAIAssistant.Services.Exceptions;

namespace ZohoAIAssistant.Controllers;

/// <summary>
/// Exposes Zoho CRM Lead data through a REST API.
/// Routes: GET /api/leads, GET /api/leads/test, GET /api/leads/{id}
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class LeadsController : ControllerBase
{
    private readonly IZohoLeadService _leadService;
    private readonly ILogger<LeadsController> _logger;

    public LeadsController(IZohoLeadService leadService, ILogger<LeadsController> logger)
    {
        _leadService = leadService;
        _logger = logger;
    }

    /// <summary>
    /// Step 6 — Returns the first 5 leads from Zoho CRM (quick test after OAuth).
    /// </summary>
    /// <remarks>
    /// **OAuth Workflow — Step 6**
    ///
    /// After completing OAuth and storing the refresh token, call this endpoint to verify
    /// lead retrieval. Returns at most 5 leads. Access tokens are refreshed automatically
    /// if expired.
    ///
    /// Example response:
    /// ```json
    /// {
    ///   "leads": [
    ///     { "id": "123456789", "firstName": "Jane", "lastName": "Doe", "email": "jane@example.com" }
    ///   ],
    ///   "count": 5,
    ///   "page": 1,
    ///   "perPage": 5,
    ///   "moreRecords": true
    /// }
    /// ```
    /// </remarks>
    /// <response code="200">First 5 leads returned successfully.</response>
    /// <response code="401">OAuth not configured — complete the OAuth flow first.</response>
    /// <response code="502">Zoho CRM API returned an error.</response>
    [HttpGet("test")]
    [ProducesResponseType(typeof(ZohoLeadListResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ZohoLeadListResponseDto>> GetTestLeads(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Leads test endpoint called — fetching first 5 leads.");
            var leads = await _leadService.GetFirstLeadsAsync(5, cancellationToken);
            return Ok(leads);
        }
        catch (ZohoApiException ex)
        {
            _logger.LogError(ex, "Leads test endpoint failed.");
            return StatusCode(ex.StatusCode, new ProblemDetails
            {
                Title = "Zoho CRM Error",
                Detail = ex.Message,
                Status = ex.StatusCode
            });
        }
    }

    /// <summary>Returns all leads from Zoho CRM (first page).</summary>
    /// <remarks>
    /// Retrieves the first page of leads. Access tokens are refreshed automatically when expired.
    /// </remarks>
    /// <response code="200">Leads returned successfully.</response>
    /// <response code="502">Zoho CRM API returned an error.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ZohoLeadListResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ZohoLeadListResponseDto>> GetAllLeads(CancellationToken cancellationToken)
    {
        try
        {
            var leads = await _leadService.GetAllLeadsAsync(cancellationToken);
            return Ok(leads);
        }
        catch (ZohoApiException ex)
        {
            _logger.LogError(ex, "Failed to fetch leads from Zoho CRM.");
            return StatusCode(ex.StatusCode, new ProblemDetails
            {
                Title = "Zoho CRM Error",
                Detail = ex.Message,
                Status = ex.StatusCode
            });
        }
    }

    /// <summary>Returns a single lead by Zoho record ID.</summary>
    /// <param name="id">Zoho CRM Lead record ID (numeric string).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Lead found and returned.</response>
    /// <response code="404">Lead not found in Zoho CRM.</response>
    /// <response code="502">Zoho CRM API returned an error.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ZohoLeadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ZohoLeadDto>> GetLeadById(string id, CancellationToken cancellationToken)
    {
        try
        {
            var lead = await _leadService.GetLeadByIdAsync(id, cancellationToken);

            if (lead is null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Lead Not Found",
                    Detail = $"No lead with ID '{id}' was found in Zoho CRM.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return Ok(lead);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Request",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (ZohoApiException ex)
        {
            _logger.LogError(ex, "Failed to fetch lead {LeadId} from Zoho CRM.", id);
            return StatusCode(ex.StatusCode, new ProblemDetails
            {
                Title = "Zoho CRM Error",
                Detail = ex.Message,
                Status = ex.StatusCode
            });
        }
    }
}
