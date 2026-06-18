namespace CryptoExchanges.Net.Core.Enums;

/// <summary>Specifies the time window for a rate limit rule.</summary>
public enum RateLimitInterval
{
    /// <summary>Per second.</summary>
    Second,
    /// <summary>Per minute.</summary>
    Minute,
    /// <summary>Per day.</summary>
    Day
}
