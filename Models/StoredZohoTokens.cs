namespace ZohoAIAssistant.Models;

/// <summary>
/// Persisted OAuth token state written to the demo JSON file and held in memory.
/// </summary>
public class StoredZohoTokens
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public DateTimeOffset? LastUpdatedUtc { get; set; }
}
