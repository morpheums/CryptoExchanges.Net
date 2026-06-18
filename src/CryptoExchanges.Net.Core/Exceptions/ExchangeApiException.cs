namespace CryptoExchanges.Net.Core.Exceptions;

/// <summary>An error returned by an exchange API (carries the venue error code when known).</summary>
public class ExchangeApiException : ExchangeException
{
    /// <summary>The venue-specific error code, when the response carried one.</summary>
    public int? Code { get; }

    /// <summary>The raw response body, for diagnostics.</summary>
    public string? RawBody { get; }

    /// <summary>Creates an API exception.</summary>
    public ExchangeApiException(string message, int? code = null, string? rawBody = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        RawBody = rawBody;
    }
}
