namespace CryptoExchanges.Net.Binance.Services;

internal sealed record TickerResponseDto
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("lastPrice")]
    public string LastPrice { get; init; } = "0";

    [JsonPropertyName("openPrice")]
    public string OpenPrice { get; init; } = "0";

    [JsonPropertyName("highPrice")]
    public string HighPrice { get; init; } = "0";

    [JsonPropertyName("lowPrice")]
    public string LowPrice { get; init; } = "0";

    [JsonPropertyName("volume")]
    public string Volume { get; init; } = "0";

    [JsonPropertyName("quoteVolume")]
    public string QuoteVolume { get; init; } = "0";

    [JsonPropertyName("priceChange")]
    public string PriceChange { get; init; } = "0";

    [JsonPropertyName("priceChangePercent")]
    public string PriceChangePercent { get; init; } = "0";

    [JsonPropertyName("closeTime")]
    public long CloseTime { get; init; }
}
