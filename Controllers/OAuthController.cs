using Microsoft.AspNetCore.Mvc;
using ZohoAIAssistant.DTOs;
using ZohoAIAssistant.Services;
using ZohoAIAssistant.Services.Exceptions;

namespace ZohoAIAssistant.Controllers;

/// <summary>
/// Manages the Zoho OAuth 2.0 authorization workflow through Swagger-friendly REST endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class OAuthController : ControllerBase
{
    private readonly IZohoOAuthService _oauthService;
    private readonly ILogger<OAuthController> _logger;

    public OAuthController(IZohoOAuthService oauthService, ILogger<OAuthController> logger)
    {
        _oauthService = oauthService;
        _logger = logger;
    }

    /// <summary>
    /// Step 1 — Generate the Zoho OAuth authorization URL.
    /// </summary>
    /// <remarks>
    /// **OAuth Workflow — Step 1**
    ///
    /// Call this endpoint to receive a Zoho authorization URL. Open the `authorizationUrl`
    /// value in a browser, sign in to Zoho, and approve access.
    ///
    /// Example response:
    /// ```json
    /// {
    ///   "authorizationUrl": "https://accounts.zoho.in/oauth/v2/auth?scope=...",
    ///   "redirectUri": "http://localhost:5000/api/oauth/callback",
    ///   "scopes": "ZohoCRM.modules.leads.READ,...",
    ///   "instructions": "Open AuthorizationUrl in a browser..."
    /// }
    /// ```
    /// </remarks>
    /// <response code="200">Authorization URL generated successfully.</response>
    /// <response code="500">Client ID or Client Secret is missing from configuration.</response>
    [HttpGet("authorize")]
    [ProducesResponseType(typeof(OAuthAuthorizeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public ActionResult<OAuthAuthorizeResponseDto> GetAuthorizationUrl()
    {
        try
        {
            _logger.LogInformation("OAuth authorize endpoint called — generating authorization URL.");
            var result = _oauthService.BuildAuthorizationUrl();
            return Ok(result);
        }
        catch (ZohoApiException ex)
        {
            _logger.LogError(ex, "Failed to generate OAuth authorization URL.");
            return StatusCode(ex.StatusCode, new ProblemDetails
            {
                Title = "OAuth Configuration Error",
                Detail = ex.Message,
                Status = ex.StatusCode
            });
        }
    }

    /// <summary>
    /// Step 4 — Exchange the authorization code for access and refresh tokens.
    /// </summary>
    /// <remarks>
    /// **OAuth Workflow — Step 4**
    ///
    /// After signing in to Zoho (Step 2), the browser is redirected to the callback URL with a
    /// `code` query parameter (Step 3). Pass that code here to obtain and store tokens.
    ///
    /// Example callback URL from the browser:
    /// ```
    /// http://localhost:5000/api/oauth/callback?code=1000.abc123.xyz&amp;location=in
    /// ```
    ///
    /// Example request:
    /// ```
    /// GET /api/oauth/callback?code=1000.abc123.xyz
    /// ```
    ///
    /// On success, the refresh token is saved to `Data/zoho-tokens.json` (Step 5).
    /// </remarks>
    /// <param name="code">Authorization code from the Zoho redirect URL (expires in ~60 seconds).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Tokens exchanged and stored successfully.</response>
    /// <response code="400">Authorization code is missing or invalid.</response>
    /// <response code="502">Zoho token endpoint returned an error.</response>
    [HttpGet("callback")]
    [ProducesResponseType(typeof(OAuthCallbackResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<OAuthCallbackResultDto>> Callback(
        [FromQuery] string code,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            _logger.LogWarning("OAuth callback invoked without an authorization code.");
            return BadRequest(new ProblemDetails
            {
                Title = "Missing Authorization Code",
                Detail = "Provide the ?code= parameter from the Zoho redirect URL. Example: /api/oauth/callback?code=1000.xxxxx",
                Status = StatusCodes.Status400BadRequest
            });
        }

        try
        {
            _logger.LogInformation("OAuth callback received — exchanging authorization code for tokens.");
            var result = await _oauthService.ExchangeAuthorizationCodeAsync(code, cancellationToken);
            return Ok(result);
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
            _logger.LogError(ex, "OAuth code exchange failed.");
            return StatusCode(ex.StatusCode, new ProblemDetails
            {
                Title = "OAuth Token Exchange Failed",
                Detail = ex.Message,
                Status = ex.StatusCode
            });
        }
    }

    /// <summary>
    /// Returns the current OAuth token status (access token, refresh token, expiry).
    /// </summary>
    /// <remarks>
    /// Example response:
    /// ```json
    /// {
    ///   "accessToken": "1000.xxxxx",
    ///   "refreshToken": "1000.yyyyy",
    ///   "expiresAtUtc": "2026-06-11T12:00:00+00:00",
    ///   "isAccessTokenValid": true,
    ///   "hasRefreshToken": true,
    ///   "message": "Access token is valid."
    /// }
    /// ```
    /// </remarks>
    /// <response code="200">Current token status returned.</response>
    [HttpGet("tokens")]
    [ProducesResponseType(typeof(ZohoTokenStatusDto), StatusCodes.Status200OK)]
    public ActionResult<ZohoTokenStatusDto> GetTokens()
    {
        _logger.LogInformation("OAuth tokens status requested.");
        return Ok(_oauthService.GetTokenStatus());
    }

    /// <summary>
    /// Step 5 — Force-refresh the access token using the stored refresh token.
    /// </summary>
    /// <remarks>
    /// **OAuth Workflow — Step 5 (optional manual refresh)**
    ///
    /// Generates a new access token from the stored refresh token. The Leads API also refreshes
    /// automatically when the access token expires.
    ///
    /// Example response:
    /// ```json
    /// {
    ///   "accessToken": "1000.newtoken",
    ///   "refreshToken": "1000.yyyyy",
    ///   "expiresAtUtc": "2026-06-11T13:00:00+00:00",
    ///   "isAccessTokenValid": true,
    ///   "hasRefreshToken": true,
    ///   "message": "Access token is valid."
    /// }
    /// ```
    /// </remarks>
    /// <response code="200">Access token refreshed successfully.</response>
    /// <response code="401">No refresh token available — complete OAuth flow first.</response>
    /// <response code="502">Zoho token endpoint returned an error.</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ZohoTokenStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ZohoTokenStatusDto>> RefreshToken(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Manual OAuth token refresh requested.");
            await _oauthService.RefreshAccessTokenAsync(cancellationToken);
            return Ok(_oauthService.GetTokenStatus());
        }
        catch (ZohoApiException ex)
        {
            _logger.LogError(ex, "Manual OAuth token refresh failed.");
            return StatusCode(ex.StatusCode, new ProblemDetails
            {
                Title = "OAuth Refresh Failed",
                Detail = ex.Message,
                Status = ex.StatusCode
            });
        }
    }
}
