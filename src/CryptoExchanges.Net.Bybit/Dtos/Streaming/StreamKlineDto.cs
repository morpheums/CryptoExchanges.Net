namespace CryptoExchanges.Net.Bybit.Dtos.Streaming;

/// <summary>
/// A single kline/candlestick entry from a Bybit v5 WebSocket kline frame
/// (<c>topic: kline.{interval}.{symbol}</c>). The <c>data</c> field is an array of these records.
/// OHLCV fields are string-encoded; <c>confirm</c> is <see langword="true"/> when the bar is closed.
/// </summary>
internal sealed record StreamKlineDto
{
    /// <summary>Kline open time (unix milliseconds).</summary>
    [JsonPropertyName("start")]
    public long OpenTime { get; init; }

    /// <summary>Open price.</summary>
    [JsonPropertyName("open")]
    public string Open { get; init; } = "0";

    /// <summary>High price.</summary>
    [JsonPropertyName("high")]
    public string High { get; init; } = "0";

    /// <summary>Low price.</summary>
    [JsonPropertyName("low")]
    public string Low { get; init; } = "0";

    /// <summary>Close price.</summary>
    [JsonPropertyName("close")]
    public string Close { get; init; } = "0";

    /// <summary>Base-asset volume.</summary>
    [JsonPropertyName("volume")]
    public string Volume { get; init; } = "0";

    /// <summary>Kline interval string (e.g. <c>"1"</c> for 1 minute).</summary>
    [JsonPropertyName("interval")]
    public string Interval { get; init; } = string.Empty;

    /// <summary><see langword="true"/> when the kline bar is closed (confirmed).</summary>
    [JsonPropertyName("confirm")]
    public bool Confirm { get; init; }
}
