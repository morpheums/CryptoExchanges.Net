namespace CryptoExchanges.Net.Okx.Dtos.Streaming;

/// <summary>
/// One element of the <c>data</c> array in an OKX WebSocket order-book frame
/// (<c>channel: books5</c> or <c>books</c>). Each price level is <c>[price, qty, liquidatedOrders, orders]</c>.
/// Symbol is sourced from <c>arg.instId</c> on the outer envelope.
/// </summary>
internal sealed record StreamDepthDto
{
    [JsonPropertyName("bids")]
    public List<List<string>> Bids { get; init; } = [];

    [JsonPropertyName("asks")]
    public List<List<string>> Asks { get; init; } = [];

    [JsonPropertyName("ts")]
    public string Ts { get; init; } = "0";

    [JsonPropertyName("seqId")]
    public long SeqId { get; init; }
}
