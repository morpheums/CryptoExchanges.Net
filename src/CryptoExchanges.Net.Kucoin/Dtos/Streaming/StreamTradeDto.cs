namespace CryptoExchanges.Net.Kucoin.Dtos.Streaming;

/// <summary>
/// The <c>data</c> payload of a KuCoin WebSocket trade/match frame
/// (<c>topic: /market/match:{symbol}</c>). Decimal fields are string-encoded.
/// </summary>
internal sealed record StreamTradeDto
{
    /// <summary>Sequence number for ordering frames.</summary>
    [JsonPropertyName("sequence")]
    public string Sequence { get; init; } = "0";

    /// <summary>Trade execution type (e.g. <c>"trade"</c>).</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>Trading symbol wire string (e.g. <c>BTC-USDT</c>).</summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Maker order ID.</summary>
    [JsonPropertyName("makerOrderId")]
    public string MakerOrderId { get; init; } = string.Empty;

    /// <summary>Taker order ID.</summary>
    [JsonPropertyName("takerOrderId")]
    public string TakerOrderId { get; init; } = string.Empty;

    /// <summary>Trade execution price.</summary>
    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";

    /// <summary>Trade size (quantity in base currency).</summary>
    [JsonPropertyName("size")]
    public string Size { get; init; } = "0";

    /// <summary>
    /// Taker order side: <c>"buy"</c> or <c>"sell"</c>.
    /// The side is from the taker's perspective.
    /// </summary>
    [JsonPropertyName("side")]
    public string Side { get; init; } = string.Empty;

    /// <summary>Unique trade ID.</summary>
    [JsonPropertyName("tradeId")]
    public string TradeId { get; init; } = string.Empty;

    /// <summary>Trade timestamp in unix nanoseconds (string-encoded).</summary>
    [JsonPropertyName("time")]
    public string Time { get; init; } = "0";
}
