namespace CryptoExchanges.Net.Core.Exceptions;

/// <summary>Authentication, signature, permission, or IP-restriction failure.</summary>
public sealed class AuthenticationException : ExchangeApiException
{
    /// <summary>Creates an authentication exception.</summary>
    public AuthenticationException(string message, int? code = null, string? rawBody = null, Exception? innerException = null)
        : base(message, code, rawBody, innerException) { }
}
