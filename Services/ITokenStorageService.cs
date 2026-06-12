using ZohoAIAssistant.DTOs;
using ZohoAIAssistant.Models;

namespace ZohoAIAssistant.Services;

/// <summary>
/// Persists Zoho OAuth tokens in memory and to a JSON file for demo purposes.
/// </summary>
public interface ITokenStorageService
{
    /// <summary>Returns the current stored token snapshot.</summary>
    Task<StoredZohoTokens> GetStoredTokensAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves access and refresh tokens to memory and the JSON file.</summary>
    Task SaveTokensAsync(
        string accessToken,
        string? refreshToken,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Returns true when a non-expired access token is available.</summary>
    bool IsAccessTokenValid();

    /// <summary>
    /// Returns the refresh token from storage, falling back to appsettings when storage is empty.
    /// </summary>
    string? GetEffectiveRefreshToken();

    /// <summary>Maps stored tokens to the API-facing status DTO.</summary>
    ZohoTokenStatusDto GetTokenStatus();
}
