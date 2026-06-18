namespace CryptoExchanges.Net.Binance.Services;

internal sealed record BinanceRateLimit
{
    [JsonPropertyName("rateLimitType")]
    public string RateLimitType { get; init; } = string.Empty;

    [JsonPropertyName("interval")]
    public string Interval { get; init; } = string.Empty;

    [JsonPropertyName("limit")]
    public int Limit { get; init; }
}
