namespace ZohoAIAssistant.DTOs;

/// <summary>Response returned by GET /api/oauth/authorize.</summary>
public class OAuthAuthorizeResponseDto
{
    /// <summary>Full Zoho authorization URL — open in a browser to sign in.</summary>
    public string AuthorizationUrl { get; set; } = string.Empty;

    /// <summary>Human-readable next steps for the OAuth workflow.</summary>
    public string Instructions { get; set; } = string.Empty;

    /// <summary>Redirect URI embedded in the authorization URL (must match Zoho client settings).</summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>OAuth scopes requested during authorization.</summary>
    public string Scopes { get; set; } = string.Empty;
}

/// <summary>Token status returned by GET /api/oauth/tokens and POST /api/oauth/refresh.</summary>
public class ZohoTokenStatusDto
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public bool IsAccessTokenValid { get; set; }
    public bool HasRefreshToken { get; set; }
    public string? Message { get; set; }
}

/// <summary>Result of GET /api/oauth/callback after exchanging the authorization code.</summary>
public class OAuthCallbackResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public ZohoTokenStatusDto? Tokens { get; set; }
}

/// <summary>Result of GET /api/health/zoho connectivity check.</summary>
public class ZohoHealthCheckDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool AccessTokenGenerated { get; set; }
    public bool CrmApiReachable { get; set; }
    public int? HttpStatusCode { get; set; }
    public DateTimeOffset CheckedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
