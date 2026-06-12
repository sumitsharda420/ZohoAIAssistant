using System.Text.Json.Serialization;

namespace ZohoAIAssistant.DTOs;

/// <summary>
/// Deserializes the JSON body returned by Zoho's OAuth token endpoint.
/// </summary>
public class ZohoTokenResponseDto
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("api_domain")]
    public string? ApiDomain { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
