namespace ZohoAIAssistant.Configuration;

/// <summary>
/// Strongly-typed configuration bound from the "OpenAI" section in appsettings.json.
/// </summary>
public class OpenAISettings
{
    public const string SectionName = "OpenAI";

    /// <summary>OpenAI API key from https://platform.openai.com/api-keys.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Chat model used for lead analysis (e.g. gpt-4o-mini).</summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>OpenAI Chat Completions API base URL.</summary>
    public string ApiBaseUrl { get; set; } = "https://api.openai.com/v1";
}
