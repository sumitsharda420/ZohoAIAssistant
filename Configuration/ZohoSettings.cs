namespace ZohoAIAssistant.Configuration;

/// <summary>
/// Strongly-typed configuration bound from the "Zoho" section in appsettings.json.
/// Holds OAuth credentials and Zoho API endpoint URLs for the India data center.
/// </summary>
public class ZohoSettings
{
    public const string SectionName = "Zoho";

    /// <summary>Zoho OAuth client ID from the API Console.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Zoho OAuth client secret from the API Console.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Long-lived refresh token obtained after the initial OAuth authorization flow.</summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>Redirect URI registered in the Zoho client; must match exactly.</summary>
    public string RedirectUri { get; set; } = "http://localhost:5000/api/oauth/callback";

    /// <summary>Relative path for demo JSON token persistence (under content root).</summary>
    public string TokenStorageFilePath { get; set; } = "Data/zoho-tokens.json";

    /// <summary>OAuth scopes requested during the authorization step.</summary>
    public string OAuthScopes { get; set; } =
        "ZohoCRM.modules.leads.READ,ZohoCRM.modules.leads.ALL,ZohoCRM.modules.ALL,ZohoCRM.settings.fields.READ";

    /// <summary>Zoho Accounts base URL (e.g. https://accounts.zoho.in for India DC).</summary>
    public string AccountsUrl { get; set; } = "https://accounts.zoho.in";

    /// <summary>Zoho CRM API base URL (e.g. https://www.zohoapis.in for India DC).</summary>
    public string ApiBaseUrl { get; set; } = "https://www.zohoapis.in";
}
