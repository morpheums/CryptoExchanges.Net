namespace CryptoExchanges.Net.Bybit.Services;

internal sealed record BybitTickerResult
{
    [JsonPropertyName("list")]
    public List<BybitTicker> List { get; init; } = [];
}
