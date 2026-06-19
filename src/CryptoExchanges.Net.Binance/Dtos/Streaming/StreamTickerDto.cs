namespace CryptoExchanges.Net.Binance.Dtos.Streaming;

/// <summary>
/// WebSocket combined-stream ticker payload (the <c>data</c> field of a <c>stream:&lt;symbol&gt;@ticker</c> frame).
/// Field names match the 24-hour price change statistics stream format.
/// </summary>
internal sealed record StreamTickerDto
{
    /// <summary>Trading symbol wire string (e.g. <c>"BTCUSDT"</c>).</summary>
    [JsonPropertyName("s")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Last price.</summary>
    [JsonPropertyName("c")]
    public string LastPrice { get; init; } = "0";

    /// <summary>Open price (first trade price of the 24-hour statistical window).</summary>
    [JsonPropertyName("o")]
    public string OpenPrice { get; init; } = "0";

    /// <summary>High price during the 24-hour window.</summary>
    [JsonPropertyName("h")]
    public string HighPrice { get; init; } = "0";

    /// <summary>Low price during the 24-hour window.</summary>
    [JsonPropertyName("l")]
    public string LowPrice { get; init; } = "0";

    /// <summary>Total traded base-asset volume.</summary>
    [JsonPropertyName("v")]
    public string Volume { get; init; } = "0";

    /// <summary>Total traded quote-asset volume.</summary>
    [JsonPropertyName("q")]
    public string QuoteVolume { get; init; } = "0";

    /// <summary>Price change over the 24-hour window.</summary>
    [JsonPropertyName("p")]
    public string PriceChange { get; init; } = "0";

    /// <summary>Price change percent over the 24-hour window.</summary>
    [JsonPropertyName("P")]
    public string PriceChangePercent { get; init; } = "0";

    /// <summary>Statistics close time (unix-millisecond timestamp).</summary>
    [JsonPropertyName("C")]
    public long CloseTime { get; init; }
}
