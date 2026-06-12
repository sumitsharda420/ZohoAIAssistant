namespace ZohoAIAssistant.Services.Exceptions;

/// <summary>
/// Thrown when a Zoho CRM or OAuth API call fails or returns an unexpected response.
/// </summary>
public class ZohoApiException : Exception
{
    public int StatusCode { get; }

    public ZohoApiException(string message, int statusCode = 500)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public ZohoApiException(string message, Exception innerException, int statusCode = 500)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
