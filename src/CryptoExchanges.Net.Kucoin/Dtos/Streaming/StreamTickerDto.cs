namespace CryptoExchanges.Net.Kucoin.Dtos.Streaming;

/// <summary>
/// The inner <c>data.data</c> payload of a KuCoin WebSocket snapshot ticker frame
/// (<c>topic: /market/snapshot:{symbol}</c>). All numeric fields arrive as JSON numbers
/// (not strings) per the snapshot channel wire format.
/// </summary>
internal sealed record StreamTickerDto
{
    /// <summary>Trading symbol wire string (e.g. <c>BTC-USDT</c>).</summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Last traded price.</summary>
    [JsonPropertyName("lastTradedPrice")]
    public decimal LastTradedPrice { get; init; }

    /// <summary>Best bid price (<c>buy</c> in the snapshot payload).</summary>
    [JsonPropertyName("buy")]
    public decimal Buy { get; init; }

    /// <summary>Best ask price (<c>sell</c> in the snapshot payload).</summary>
    [JsonPropertyName("sell")]
    public decimal Sell { get; init; }

    /// <summary>24h high price.</summary>
    [JsonPropertyName("high")]
    public decimal High { get; init; }

    /// <summary>24h low price.</summary>
    [JsonPropertyName("low")]
    public decimal Low { get; init; }

    /// <summary>Opening price (price 24 hours ago).</summary>
    [JsonPropertyName("open")]
    public decimal Open { get; init; }

    /// <summary>24h base-asset volume.</summary>
    [JsonPropertyName("vol")]
    public decimal Vol { get; init; }

    /// <summary>24h quote-asset volume.</summary>
    [JsonPropertyName("volValue")]
    public decimal VolValue { get; init; }

    /// <summary>Absolute price change over the last 24 hours.</summary>
    [JsonPropertyName("changePrice")]
    public decimal ChangePrice { get; init; }

    /// <summary>
    /// Fractional price-change rate over the last 24 hours (e.g. <c>0.0014</c> ≡ 0.14%).
    /// Multiply by 100 to obtain a percentage.
    /// </summary>
    [JsonPropertyName("changeRate")]
    public decimal ChangeRate { get; init; }

    /// <summary>Frame timestamp in unix milliseconds (JSON number).</summary>
    [JsonPropertyName("datetime")]
    public long Datetime { get; init; }
}
