namespace CryptoExchanges.Net.Kraken.Dtos;

/// <summary>
/// Kraken OHLC (kline) row as returned by <c>/0/public/OHLC</c>.
/// Kraken returns rows as positional arrays: [time, open, high, low, close, vwap, volume, count].
/// This DTO wraps one deserialized row via positional index properties.
/// </summary>
internal sealed record CandlestickDto
{
    /// <summary>Open time in unix seconds.</summary>
    [JsonPropertyName("0")]
    public long OpenTime { get; init; }

    /// <summary>Open price.</summary>
    [JsonPropertyName("1")]
    public string Open { get; init; } = "0";

    /// <summary>High price.</summary>
    [JsonPropertyName("2")]
    public string High { get; init; } = "0";

    /// <summary>Low price.</summary>
    [JsonPropertyName("3")]
    public string Low { get; init; } = "0";

    /// <summary>Close price.</summary>
    [JsonPropertyName("4")]
    public string Close { get; init; } = "0";

    /// <summary>Volume-weighted average price.</summary>
    [JsonPropertyName("5")]
    public string Vwap { get; init; } = "0";

    /// <summary>Volume in base currency.</summary>
    [JsonPropertyName("6")]
    public string Volume { get; init; } = "0";

    /// <summary>Trade count for the interval.</summary>
    [JsonPropertyName("7")]
    public int Count { get; init; }
}
