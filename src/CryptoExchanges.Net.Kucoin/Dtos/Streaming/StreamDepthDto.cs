namespace CryptoExchanges.Net.Kucoin.Dtos.Streaming;

/// <summary>
/// The <c>data</c> payload of a KuCoin WebSocket order-book update frame
/// (<c>topic: /market/level2:{symbol}</c>). Delivers incremental changes; prices and
/// sizes are string-encoded. Each entry is <c>[price, size]</c> as strings.
/// </summary>
internal sealed record StreamDepthDto
{
    /// <summary>Sequence number at the start of this change.</summary>
    [JsonPropertyName("sequenceStart")]
    public long SequenceStart { get; init; }

    /// <summary>Sequence number at the end of this change.</summary>
    [JsonPropertyName("sequenceEnd")]
    public long SequenceEnd { get; init; }

    /// <summary>Trading symbol wire string (e.g. <c>BTC-USDT</c>).</summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>
    /// Changed bid levels: each inner list is <c>[price, size]</c> as strings.
    /// A size of <c>"0"</c> means the level was removed.
    /// </summary>
    [JsonPropertyName("changes")]
    public DepthChangesDto Changes { get; init; } = new();
}

/// <summary>
/// The nested changes object inside <see cref="StreamDepthDto"/>, carrying bid and ask level changes.
/// </summary>
internal sealed record DepthChangesDto
{
    /// <summary>
    /// Changed ask levels: each inner list is <c>[price, size, sequence]</c> as strings.
    /// </summary>
    [JsonPropertyName("asks")]
    public List<List<string>> Asks { get; init; } = [];

    /// <summary>
    /// Changed bid levels: each inner list is <c>[price, size, sequence]</c> as strings.
    /// </summary>
    [JsonPropertyName("bids")]
    public List<List<string>> Bids { get; init; } = [];
}
