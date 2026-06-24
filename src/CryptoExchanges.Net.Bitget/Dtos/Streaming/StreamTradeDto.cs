namespace CryptoExchanges.Net.Bitget.Dtos.Streaming;

/// <summary>
/// A single trade entry from a Bitget v2 WebSocket public-trade frame (<c>channel: trade</c>).
/// Symbol is resolved from <c>arg.instId</c> on the outer envelope.
/// </summary>
internal sealed record StreamTradeDto
{
    /// <summary>Trade execution timestamp (unix milliseconds, string-encoded).</summary>
    [JsonPropertyName("ts")]
    public string Ts { get; init; } = "0";

    /// <summary>Trade execution price.</summary>
    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";

    /// <summary>Trade size (quantity in base currency).</summary>
    [JsonPropertyName("size")]
    public string Size { get; init; } = "0";

    /// <summary>Taker order side: <c>"buy"</c> or <c>"sell"</c>.</summary>
    [JsonPropertyName("side")]
    public string Side { get; init; } = string.Empty;

    /// <summary>Unique trade ID.</summary>
    [JsonPropertyName("tradeId")]
    public string TradeId { get; init; } = string.Empty;
}
