namespace CryptoExchanges.Net.Binance.Dtos.Streaming;

/// <summary>
/// WebSocket combined-stream order-book depth payload (the <c>data</c> field of a
/// <c>stream:&lt;symbol&gt;@depth</c> diff-depth frame or a depth snapshot).
/// v1 delivers raw per-frame snapshots; no local-book maintenance is performed.
/// </summary>
internal sealed record StreamDepthDto
{
    /// <summary>
    /// Trading symbol wire string (e.g. <c>"BTCUSDT"</c>).
    /// Present in diff-depth frames; absent in partial-book-depth frames.
    /// </summary>
    [JsonPropertyName("s")]
    public string? Symbol { get; init; }

    /// <summary>Last-update sequence ID for consumer-side gap detection.</summary>
    [JsonPropertyName("lastUpdateId")]
    public long LastUpdateId { get; init; }

    /// <summary>
    /// Bid price levels: each inner list is <c>[price, quantity]</c> as strings.
    /// </summary>
    [JsonPropertyName("bids")]
    public List<List<string>> Bids { get; init; } = [];

    /// <summary>
    /// Ask price levels: each inner list is <c>[price, quantity]</c> as strings.
    /// </summary>
    [JsonPropertyName("asks")]
    public List<List<string>> Asks { get; init; } = [];
}
