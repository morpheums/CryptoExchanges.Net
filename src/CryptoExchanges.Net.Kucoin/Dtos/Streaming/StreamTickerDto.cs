namespace CryptoExchanges.Net.Kucoin.Dtos.Streaming;

/// <summary>
/// The <c>data</c> payload of a KuCoin WebSocket ticker frame
/// (<c>topic: /market/ticker:{symbol}</c>). Decimal fields are string-encoded per
/// the KuCoin V2 API convention.
/// </summary>
internal sealed record StreamTickerDto
{
    /// <summary>Sequence number for ordering frames.</summary>
    [JsonPropertyName("sequence")]
    public string Sequence { get; init; } = "0";

    /// <summary>Last traded price.</summary>
    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";

    /// <summary>Best bid price.</summary>
    [JsonPropertyName("bestBid")]
    public string BestBid { get; init; } = "0";

    /// <summary>Best bid size.</summary>
    [JsonPropertyName("bestBidSize")]
    public string BestBidSize { get; init; } = "0";

    /// <summary>Best ask price.</summary>
    [JsonPropertyName("bestAsk")]
    public string BestAsk { get; init; } = "0";

    /// <summary>Best ask size.</summary>
    [JsonPropertyName("bestAskSize")]
    public string BestAskSize { get; init; } = "0";

    /// <summary>24h open price (price 24 hours ago).</summary>
    [JsonPropertyName("open")]
    public string Open { get; init; } = "0";

    /// <summary>24h high price.</summary>
    [JsonPropertyName("high")]
    public string High { get; init; } = "0";

    /// <summary>24h low price.</summary>
    [JsonPropertyName("low")]
    public string Low { get; init; } = "0";

    /// <summary>24h base-asset volume.</summary>
    [JsonPropertyName("vol")]
    public string Vol { get; init; } = "0";

    /// <summary>24h quote-asset volume.</summary>
    [JsonPropertyName("volValue")]
    public string VolValue { get; init; } = "0";

    /// <summary>Trading symbol wire string (e.g. <c>BTC-USDT</c>).</summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Frame timestamp in unix nanoseconds (string-encoded).</summary>
    [JsonPropertyName("time")]
    public string Time { get; init; } = "0";
}
