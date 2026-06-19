namespace CryptoExchanges.Net.Binance.Dtos.Streaming;

/// <summary>
/// WebSocket combined-stream trade payload (the <c>data</c> field of a <c>stream:&lt;symbol&gt;@trade</c> frame).
/// Field names match the individual symbol trade stream format.
/// </summary>
internal sealed record StreamTradeDto
{
    /// <summary>Trading symbol wire string.</summary>
    [JsonPropertyName("s")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Trade ID.</summary>
    [JsonPropertyName("t")]
    public long TradeId { get; init; }

    /// <summary>Trade price.</summary>
    [JsonPropertyName("p")]
    public string Price { get; init; } = "0";

    /// <summary>Trade quantity.</summary>
    [JsonPropertyName("q")]
    public string Quantity { get; init; } = "0";

    /// <summary>Trade execution time (unix-millisecond timestamp).</summary>
    [JsonPropertyName("T")]
    public long TradeTime { get; init; }

    /// <summary><see langword="true"/> if the buyer is the market maker.</summary>
    [JsonPropertyName("m")]
    public bool IsBuyerMaker { get; init; }
}
