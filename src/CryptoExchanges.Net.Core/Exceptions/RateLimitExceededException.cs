namespace CryptoExchanges.Net.Core.Exceptions;

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
