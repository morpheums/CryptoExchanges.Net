namespace CryptoExchanges.Net.Kucoin.Dtos;

/// <summary>
/// KuCoin kline/candlestick data as returned by <c>/api/v1/market/candles</c>.
/// KuCoin returns candlesticks as an array of string arrays:
/// <c>[timestamp, open, close, high, low, volume, quoteVolume]</c>.
/// This DTO wraps one row after deserialization from the outer <c>data</c> array.
/// </summary>
internal sealed record CandlestickDto
{
    /// <summary>Open time as unix seconds (string-encoded).</summary>
    [JsonPropertyName("0")]
    public string OpenTime { get; init; } = "0";

    /// <summary>Open price.</summary>
    [JsonPropertyName("1")]
    public string Open { get; init; } = "0";

    /// <summary>Close price.</summary>
    [JsonPropertyName("2")]
    public string Close { get; init; } = "0";

    /// <summary>High price.</summary>
    [JsonPropertyName("3")]
    public string High { get; init; } = "0";

    /// <summary>Low price.</summary>
    [JsonPropertyName("4")]
    public string Low { get; init; } = "0";

    /// <summary>Volume in base currency.</summary>
    [JsonPropertyName("5")]
    public string Volume { get; init; } = "0";

    /// <summary>Volume in quote currency (turnover).</summary>
    [JsonPropertyName("6")]
    public string QuoteVolume { get; init; } = "0";
}
