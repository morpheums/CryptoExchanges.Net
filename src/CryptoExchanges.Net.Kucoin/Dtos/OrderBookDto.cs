namespace CryptoExchanges.Net.Kucoin.Dtos;

/// <summary>
/// KuCoin order book snapshot as returned by <c>/api/v1/market/orderbook/level2_20</c>
/// and <c>/api/v1/market/orderbook/level2_100</c>. Each level is <c>["price","size"]</c>.
/// </summary>
internal sealed record OrderBookDto
{
    /// <summary>Ask levels, each entry being <c>["price", "size"]</c>.</summary>
    [JsonPropertyName("asks")]
    public List<List<string>> Asks { get; init; } = [];

    /// <summary>Bid levels, each entry being <c>["price", "size"]</c>.</summary>
    [JsonPropertyName("bids")]
    public List<List<string>> Bids { get; init; } = [];

    /// <summary>Snapshot timestamp in unix milliseconds.</summary>
    [JsonPropertyName("time")]
    public long Time { get; init; }
}
