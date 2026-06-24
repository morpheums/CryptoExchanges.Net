namespace CryptoExchanges.Net.Bybit.Dtos.Streaming;

/// <summary>
/// The <c>data</c> payload of a Bybit v5 WebSocket order-book frame
/// (<c>topic: orderbook.{depth}.{symbol}</c>). Delivers a snapshot or delta depending on
/// the <c>type</c> field of the outer envelope; both shapes share this DTO.
/// Price levels are string-encoded <c>[price, qty]</c> pairs.
/// </summary>
internal sealed record StreamDepthDto
{
    /// <summary>Trading symbol wire string (e.g. <c>"BTCUSDT"</c>).</summary>
    [JsonPropertyName("s")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>
    /// Bid price levels: each inner list is <c>[price, quantity]</c> as strings.
    /// </summary>
    [JsonPropertyName("b")]
    public List<List<string>> Bids { get; init; } = [];

    /// <summary>
    /// Ask price levels: each inner list is <c>[price, quantity]</c> as strings.
    /// </summary>
    [JsonPropertyName("a")]
    public List<List<string>> Asks { get; init; } = [];

    /// <summary>Update ID for consumer-side gap detection.</summary>
    [JsonPropertyName("u")]
    public long UpdateId { get; init; }

    /// <summary>Cross-sequence number for ordering frames across topics.</summary>
    [JsonPropertyName("seq")]
    public long Seq { get; init; }
}
