namespace CryptoExchanges.Net.Binance.Services;

internal sealed record BinanceBalance
{
    [JsonPropertyName("asset")]
    public string Asset { get; init; } = string.Empty;

    [JsonPropertyName("free")]
    public string Free { get; init; } = "0";

    [JsonPropertyName("locked")]
    public string Locked { get; init; } = "0";
}
