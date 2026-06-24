namespace CryptoExchanges.Net.Bybit.Dtos.Streaming;

/// <summary>
/// A single trade entry from a Bybit v5 WebSocket public-trade frame
/// (<c>topic: publicTrade.{symbol}</c>). The <c>data</c> field is an array of these records.
/// All decimal fields are string-encoded per the v5 wire format.
/// <c>S == "Sell"</c> means the taker was a seller, i.e. the buyer was the market maker.
/// </summary>
internal sealed record StreamTradeDto
{
    /// <summary>Trade execution timestamp (unix milliseconds).</summary>
    [JsonPropertyName("T")]
    public long TradeTime { get; init; }

    /// <summary>Trading symbol wire string (e.g. <c>"BTCUSDT"</c>).</summary>
    [JsonPropertyName("s")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>
    /// Taker order side: <c>"Buy"</c> or <c>"Sell"</c>.
    /// <c>"Sell"</c> means the buyer was the market maker.
    /// </summary>
    [JsonPropertyName("S")]
    public string Side { get; init; } = string.Empty;

    /// <summary>Trade size (quantity in base currency).</summary>
    [JsonPropertyName("v")]
    public string Quantity { get; init; } = "0";

    /// <summary>Trade execution price.</summary>
    [JsonPropertyName("p")]
    public string Price { get; init; } = "0";

    /// <summary>Unique trade ID.</summary>
    [JsonPropertyName("i")]
    public string TradeId { get; init; } = string.Empty;
}
