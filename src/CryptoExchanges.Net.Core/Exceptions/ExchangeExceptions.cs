namespace CryptoExchanges.Net.Core.Exceptions;

/// <summary>Base type for all exchange-related failures surfaced by the SDK.</summary>
public abstract class ExchangeException : Exception
{
    /// <summary>Creates an exchange exception.</summary>
    protected ExchangeException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}

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

/// <summary>The exchange rejected the request for exceeding its rate limits.</summary>
public sealed class RateLimitExceededException : ExchangeApiException
{
    /// <summary>How long the exchange asked the caller to wait, when provided.</summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>Creates a rate-limit exception.</summary>
    public RateLimitExceededException(
        string message, TimeSpan? retryAfter = null, int? code = null, string? rawBody = null, Exception? innerException = null)
        : base(message, code, rawBody, innerException) => RetryAfter = retryAfter;
}

/// <summary>Authentication, signature, permission, or IP-restriction failure.</summary>
public sealed class AuthenticationException : ExchangeApiException
{
    /// <summary>Creates an authentication exception.</summary>
    public AuthenticationException(string message, int? code = null, string? rawBody = null, Exception? innerException = null)
        : base(message, code, rawBody, innerException) { }
}

/// <summary>The exchange rejected an order (parameters, filters, or unknown order).</summary>
public sealed class InvalidOrderException : ExchangeApiException
{
    /// <summary>Creates an invalid-order exception.</summary>
    public InvalidOrderException(string message, int? code = null, string? rawBody = null, Exception? innerException = null)
        : base(message, code, rawBody, innerException) { }
}

/// <summary>The account had insufficient balance for the requested operation.</summary>
public sealed class InsufficientBalanceException : ExchangeApiException
{
    /// <summary>Creates an insufficient-balance exception.</summary>
    public InsufficientBalanceException(string message, int? code = null, string? rawBody = null, Exception? innerException = null)
        : base(message, code, rawBody, innerException) { }
}

/// <summary>
/// A transport/connectivity failure that persisted after retries were exhausted.
/// For a failed mutation (e.g. order placement) the operation's outcome may be unknown.
/// </summary>
public sealed class ExchangeConnectivityException : ExchangeException
{
    /// <summary>True when a mutating request failed and its server-side outcome is unknown.</summary>
    public bool OperationOutcomeIndeterminate { get; }

    /// <summary>Creates a connectivity exception.</summary>
    public ExchangeConnectivityException(
        string message, bool operationOutcomeIndeterminate = false, Exception? innerException = null)
        : base(message, innerException) => OperationOutcomeIndeterminate = operationOutcomeIndeterminate;
}
