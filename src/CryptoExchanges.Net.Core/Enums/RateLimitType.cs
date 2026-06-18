namespace CryptoExchanges.Net.Core.Enums;

/// <summary>Specifies the category of a rate limit rule.</summary>
public enum RateLimitType
{
    /// <summary>Request weight-based limit.</summary>
    RequestWeight,
    /// <summary>Order placement rate limit.</summary>
    Orders,
    /// <summary>Raw request count limit.</summary>
    RawRequests
}
