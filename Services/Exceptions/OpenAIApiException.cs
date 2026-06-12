namespace ZohoAIAssistant.Services.Exceptions;

/// <summary>
/// Thrown when an OpenAI API call fails or returns an unexpected response.
/// </summary>
public class OpenAIApiException : Exception
{
    public int StatusCode { get; }

    public OpenAIApiException(string message, int statusCode = 502)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public OpenAIApiException(string message, Exception innerException, int statusCode = 502)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
