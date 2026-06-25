namespace CryptoExchanges.Net.Kraken.Dtos;

/// <summary>
/// Kraken order book (Depth) as returned by <c>/0/public/Depth</c>.
/// Each entry is a positional array: [price, volume, timestamp].
/// </summary>
internal sealed record OrderBookDto
{
    [JsonPropertyName("asks")]
    public List<List<JsonElement>> Asks { get; init; } = [];

    [JsonPropertyName("bids")]
    public List<List<JsonElement>> Bids { get; init; } = [];
}
