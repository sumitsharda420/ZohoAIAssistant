using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using ZohoAIAssistant.Configuration;
using ZohoAIAssistant.DTOs;
using ZohoAIAssistant.Services.Exceptions;

namespace ZohoAIAssistant.Services;

/// <summary>
/// Handles the full Zoho OAuth 2.0 lifecycle: authorization URL generation, code exchange,
/// token caching via <see cref="ITokenStorageService"/>, and automatic refresh.
/// </summary>
public class ZohoOAuthService : IZohoOAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenStorageService _tokenStorage;
    private readonly ZohoSettings _settings;
    private readonly ILogger<ZohoOAuthService> _logger;

    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    // Refresh 60 seconds before actual expiry to avoid edge-case 401s.
    private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromSeconds(60);

    public ZohoOAuthService(
        IHttpClientFactory httpClientFactory,
        ITokenStorageService tokenStorage,
        IOptions<ZohoSettings> settings,
        ILogger<ZohoOAuthService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _tokenStorage = tokenStorage;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public OAuthAuthorizeResponseDto BuildAuthorizationUrl()
    {
        ValidateClientCredentials();

        // Zoho authorization endpoint: GET {AccountsUrl}/oauth/v2/auth
        var scopes = _settings.OAuthScopes;
        var authorizationUrl = QueryHelpers.AddQueryString(
            $"{_settings.AccountsUrl.TrimEnd('/')}/oauth/v2/auth",
            new Dictionary<string, string?>
            {
                ["scope"] = scopes,
                ["client_id"] = _settings.ClientId,
                ["response_type"] = "code",
                ["access_type"] = "offline",
                ["prompt"] = "consent",
                ["redirect_uri"] = _settings.RedirectUri
            });

        _logger.LogInformation(
            "Generated Zoho authorization URL with redirect URI {RedirectUri}.",
            _settings.RedirectUri);

        return new OAuthAuthorizeResponseDto
        {
            AuthorizationUrl = authorizationUrl,
            RedirectUri = _settings.RedirectUri,
            Scopes = scopes,
            Instructions =
                "Open AuthorizationUrl in a browser, sign in to Zoho, and approve access. " +
                "After consent, Zoho redirects to the callback URL with a ?code= parameter. " +
                "Use GET /api/oauth/callback?code={code} to exchange it for tokens."
        };
    }

    /// <inheritdoc />
    public async Task<OAuthCallbackResultDto> ExchangeAuthorizationCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Authorization code cannot be empty.", nameof(code));
        }

        ValidateClientCredentials();

        _logger.LogInformation("Exchanging Zoho authorization code for tokens.");

        var tokenUrl = $"{_settings.AccountsUrl.TrimEnd('/')}/oauth/v2/token";

        var requestBody = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = _settings.ClientId,
            ["client_secret"] = _settings.ClientSecret,
            ["redirect_uri"] = _settings.RedirectUri,
            ["code"] = code
        };

        var tokenResponse = await RequestTokensAsync(tokenUrl, requestBody, cancellationToken);

        await _tokenStorage.SaveTokensAsync(
            tokenResponse.AccessToken!,
            tokenResponse.RefreshToken,
            CalculateExpiresAt(tokenResponse.ExpiresIn),
            cancellationToken);

        _logger.LogInformation("Authorization code exchanged successfully. Refresh token stored.");

        return new OAuthCallbackResultDto
        {
            Success = true,
            Message = "Tokens obtained and stored successfully. Refresh token is saved for future use.",
            Tokens = _tokenStorage.GetTokenStatus()
        };
    }

    /// <inheritdoc />
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_tokenStorage.IsAccessTokenValid())
        {
            var stored = await _tokenStorage.GetStoredTokensAsync(cancellationToken);
            _logger.LogDebug("Returning valid access token from storage (expires {ExpiresAt:O}).", stored.ExpiresAtUtc);
            return stored.AccessToken!;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_tokenStorage.IsAccessTokenValid())
            {
                var stored = await _tokenStorage.GetStoredTokensAsync(cancellationToken);
                return stored.AccessToken!;
            }

            return await RefreshAccessTokenAsync(cancellationToken);
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<string> RefreshAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        ValidateClientCredentials();

        var refreshToken = _tokenStorage.GetEffectiveRefreshToken();
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ZohoApiException(
                "No refresh token available. Complete OAuth via GET /api/oauth/authorize first.",
                (int)HttpStatusCode.Unauthorized);
        }

        _logger.LogInformation("Requesting a new Zoho access token using the refresh token.");

        var tokenUrl = $"{_settings.AccountsUrl.TrimEnd('/')}/oauth/v2/token";

        var requestBody = new Dictionary<string, string>
        {
            ["refresh_token"] = refreshToken,
            ["client_id"] = _settings.ClientId,
            ["client_secret"] = _settings.ClientSecret,
            ["grant_type"] = "refresh_token"
        };

        var tokenResponse = await RequestTokensAsync(tokenUrl, requestBody, cancellationToken);

        await _tokenStorage.SaveTokensAsync(
            tokenResponse.AccessToken!,
            tokenResponse.RefreshToken,
            CalculateExpiresAt(tokenResponse.ExpiresIn),
            cancellationToken);

        _logger.LogInformation(
            "Zoho access token refreshed successfully. Expires in {ExpiresIn} seconds.",
            tokenResponse.ExpiresIn > 0 ? tokenResponse.ExpiresIn : 3600);

        return tokenResponse.AccessToken!;
    }

    /// <inheritdoc />
    public ZohoTokenStatusDto GetTokenStatus() => _tokenStorage.GetTokenStatus();

    /// <summary>POSTs to the Zoho token endpoint and parses the response.</summary>
    private async Task<ZohoTokenResponseDto> RequestTokensAsync(
        string tokenUrl,
        Dictionary<string, string> requestBody,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("ZohoOAuth");

        using var content = new FormUrlEncodedContent(requestBody);
        using var response = await client.PostAsync(tokenUrl, content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Zoho token request failed with status {StatusCode}. Response: {Response}",
                response.StatusCode,
                responseBody);

            throw new ZohoApiException(
                $"Zoho token request failed: {response.StatusCode}. {responseBody}",
                (int)response.StatusCode);
        }

        var tokenResponse = JsonSerializer.Deserialize<ZohoTokenResponseDto>(responseBody);

        if (tokenResponse is null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
        {
            var errorDetail = tokenResponse?.Error ?? responseBody;
            _logger.LogError("Zoho token response did not contain an access token. Detail: {Detail}", errorDetail);
            throw new ZohoApiException($"Zoho token response invalid: {errorDetail}", (int)HttpStatusCode.BadGateway);
        }

        return tokenResponse;
    }

    private static DateTimeOffset CalculateExpiresAt(int expiresInSeconds)
    {
        var expiresIn = expiresInSeconds > 0 ? expiresInSeconds : 3600;
        return DateTimeOffset.UtcNow.AddSeconds(expiresIn).Subtract(ExpiryBuffer);
    }

    private void ValidateClientCredentials()
    {
        if (string.IsNullOrWhiteSpace(_settings.ClientId)
            || string.IsNullOrWhiteSpace(_settings.ClientSecret))
        {
            throw new ZohoApiException(
                "Zoho OAuth configuration is incomplete. Set ClientId and ClientSecret in appsettings.json.",
                (int)HttpStatusCode.InternalServerError);
        }
    }
}
