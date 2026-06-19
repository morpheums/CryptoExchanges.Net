namespace CryptoExchanges.Net.Binance.Dtos.Streaming;

/// <summary>
/// WebSocket combined-stream kline/candlestick payload (the <c>data</c> field of a
/// <c>stream:&lt;symbol&gt;@kline_&lt;interval&gt;</c> frame).
/// The kline data is nested inside a <c>k</c> object.
/// </summary>
internal sealed record StreamKlineDto
{
    /// <summary>Trading symbol wire string.</summary>
    [JsonPropertyName("s")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>The kline bar data.</summary>
    [JsonPropertyName("k")]
    public StreamKlineBarDto Kline { get; init; } = new();
}

/// <summary>
/// The nested kline bar inside a <see cref="StreamKlineDto"/> payload.
/// </summary>
internal sealed record StreamKlineBarDto
{
    /// <summary>Kline open time (unix-millisecond timestamp).</summary>
    [JsonPropertyName("t")]
    public long OpenTime { get; init; }

    /// <summary>Kline close time (unix-millisecond timestamp).</summary>
    [JsonPropertyName("T")]
    public long CloseTime { get; init; }

    /// <summary>Open price.</summary>
    [JsonPropertyName("o")]
    public string Open { get; init; } = "0";

    /// <summary>High price.</summary>
    [JsonPropertyName("h")]
    public string High { get; init; } = "0";

    /// <summary>Low price.</summary>
    [JsonPropertyName("l")]
    public string Low { get; init; } = "0";

    /// <summary>Close price.</summary>
    [JsonPropertyName("c")]
    public string Close { get; init; } = "0";

    /// <summary>Base asset volume.</summary>
    [JsonPropertyName("v")]
    public string Volume { get; init; } = "0";

    /// <summary>Quote asset volume.</summary>
    [JsonPropertyName("q")]
    public string QuoteVolume { get; init; } = "0";

    /// <summary>Number of trades in the kline window.</summary>
    [JsonPropertyName("n")]
    public int TradeCount { get; init; }

    /// <summary>Kline interval string (e.g. <c>"1m"</c>).</summary>
    [JsonPropertyName("i")]
    public string Interval { get; init; } = string.Empty;
}
