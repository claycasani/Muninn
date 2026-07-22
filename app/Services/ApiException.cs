namespace Muninn.Services;

/// <summary>
/// Thrown by ApiService when the backend returns a non-2xx response.
/// Message is safe to display directly to the user.
/// </summary>
public class ApiException : Exception
{
    public int StatusCode { get; }

    public ApiException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}
