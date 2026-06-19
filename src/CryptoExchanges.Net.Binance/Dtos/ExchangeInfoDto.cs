namespace CryptoExchanges.Net.Binance.Services;

internal sealed record ExchangeInfoDto
{
    [JsonPropertyName("symbols")]
    public List<SymbolInfoDto> Symbols { get; init; } = [];

    [JsonPropertyName("rateLimits")]
    public List<RateLimitDto> RateLimits { get; init; } = [];
}
