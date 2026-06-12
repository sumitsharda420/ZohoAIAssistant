using ZohoAIAssistant.DTOs;

namespace ZohoAIAssistant.Services;

/// <summary>
/// Manages Zoho OAuth 2.0 authorization, token exchange, and automatic refresh.
/// </summary>
public interface IZohoOAuthService
{
    /// <summary>Builds the Zoho authorization URL for the browser-based OAuth flow.</summary>
    OAuthAuthorizeResponseDto BuildAuthorizationUrl();

    /// <summary>
    /// Exchanges an authorization code for access and refresh tokens and persists them.
    /// </summary>
    Task<OAuthCallbackResultDto> ExchangeAuthorizationCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a valid access token, refreshing automatically when expired or missing.
    /// </summary>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces a new access token to be generated from the refresh token.
    /// </summary>
    Task<string> RefreshAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the current token status from storage.</summary>
    ZohoTokenStatusDto GetTokenStatus();
}
