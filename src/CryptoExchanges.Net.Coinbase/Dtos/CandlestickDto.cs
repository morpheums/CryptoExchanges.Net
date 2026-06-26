namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>
/// A single candlestick entry from <c>/api/v3/brokerage/market/products/{product_id}/candles</c>.
/// Coinbase returns candles as objects with unix-second timestamps.
/// </summary>
internal sealed record CandlestickDto
{
    /// <summary>Open time as unix seconds (string-encoded).</summary>
    [JsonPropertyName("start")]
    public string Start { get; init; } = "0";

    [JsonPropertyName("open")]
    public string Open { get; init; } = "0";

    [JsonPropertyName("high")]
    public string High { get; init; } = "0";

    [JsonPropertyName("low")]
    public string Low { get; init; } = "0";

    [JsonPropertyName("close")]
    public string Close { get; init; } = "0";

    [JsonPropertyName("volume")]
    public string Volume { get; init; } = "0";
}
