using Microsoft.AspNetCore.Mvc;
using ZohoAIAssistant.DTOs;
using ZohoAIAssistant.Services;

namespace ZohoAIAssistant.Controllers;

/// <summary>
/// Health and connectivity checks for external integrations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private readonly IZohoHealthService _zohoHealthService;
    private readonly ILogger<HealthController> _logger;

    public HealthController(IZohoHealthService zohoHealthService, ILogger<HealthController> logger)
    {
        _zohoHealthService = zohoHealthService;
        _logger = logger;
    }

    /// <summary>
    /// Verifies Zoho OAuth and CRM API connectivity.
    /// </summary>
    /// <remarks>
    /// Performs a live check:
    /// 1. Obtains a valid access token (auto-refreshes if expired).
    /// 2. Calls `GET /crm/v7/Leads?per_page=1` on Zoho CRM.
    /// 3. Returns success or failure with diagnostic details.
    ///
    /// Example success response:
    /// ```json
    /// {
    ///   "success": true,
    ///   "message": "Zoho OAuth and CRM API are working correctly.",
    ///   "accessTokenGenerated": true,
    ///   "crmApiReachable": true,
    ///   "httpStatusCode": 200,
    ///   "checkedAtUtc": "2026-06-11T10:00:00+00:00"
    /// }
    /// ```
    /// </remarks>
    /// <response code="200">Health check completed (inspect `success` field for pass/fail).</response>
    [HttpGet("zoho")]
    [ProducesResponseType(typeof(ZohoHealthCheckDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ZohoHealthCheckDto>> CheckZoho(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Zoho health check endpoint invoked.");
        var result = await _zohoHealthService.CheckZohoConnectivityAsync(cancellationToken);
        return Ok(result);
    }
}
