namespace CryptoExchanges.Net.Binance.Services;

internal sealed record BinanceExchangeInfoResponse
{
    [JsonPropertyName("symbols")]
    public List<BinanceSymbolInfo> Symbols { get; init; } = [];

    [JsonPropertyName("rateLimits")]
    public List<BinanceRateLimit> RateLimits { get; init; } = [];
}
