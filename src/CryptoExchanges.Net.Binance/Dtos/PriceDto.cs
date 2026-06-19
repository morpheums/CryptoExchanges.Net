namespace CryptoExchanges.Net.Binance.Services;

internal sealed record PriceDto
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";
}
