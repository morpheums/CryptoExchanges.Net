namespace CryptoExchanges.Net.Bitget.Dtos.Streaming;

/// <summary>
/// The <c>data</c> payload of a Bitget v2 WebSocket order-book frame (<c>channel: books5 / books15</c>).
/// Price levels are string-encoded <c>[price, qty]</c> pairs. Symbol is resolved from <c>arg.instId</c>.
/// </summary>
internal sealed record StreamDepthDto
{
    /// <summary>Bid price levels: each inner list is <c>[price, quantity]</c> as strings.</summary>
    [JsonPropertyName("bids")]
    public List<List<string>> Bids { get; init; } = [];

    /// <summary>Ask price levels: each inner list is <c>[price, quantity]</c> as strings.</summary>
    [JsonPropertyName("asks")]
    public List<List<string>> Asks { get; init; } = [];

    /// <summary>Book timestamp (unix milliseconds, string-encoded).</summary>
    [JsonPropertyName("ts")]
    public string Ts { get; init; } = "0";

    /// <summary>Sequence number for ordering frames.</summary>
    [JsonPropertyName("seqId")]
    public long SeqId { get; init; }
}
