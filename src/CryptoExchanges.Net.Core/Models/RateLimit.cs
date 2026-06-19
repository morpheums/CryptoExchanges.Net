namespace CryptoExchanges.Net.Core.Models;

/// <summary>Rate limit configuration for exchange API endpoints.</summary>
public sealed record RateLimit(
    Enums.RateLimitType RateLimitType,
    Enums.RateLimitInterval Interval,
    int Limit);
