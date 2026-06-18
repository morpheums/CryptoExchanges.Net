namespace CryptoExchanges.Net.Binance.Services;

internal sealed record BinanceAccountResponse
{
    [JsonPropertyName("balances")]
    public List<BinanceBalance> Balances { get; init; } = [];
}
