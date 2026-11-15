namespace RaceDay.Web.Services;

/// <summary>A problem talking to the API, with a message that is safe to show to the user.</summary>
public class ApiException : Exception
{
    public int StatusCode { get; }
    public ApiException(string message, int statusCode = 400) : base(message) => StatusCode = statusCode;
}

/// <summary>The API says the session is no longer valid (expired or never logged in).</summary>
public class ApiUnauthorizedException : ApiException
{
    public ApiUnauthorizedException() : base("Your session has expired. Please log in again.", 401) { }
}
