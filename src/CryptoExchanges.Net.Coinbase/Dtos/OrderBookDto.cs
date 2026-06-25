namespace CryptoExchanges.Net.Coinbase.Dtos;

internal sealed record OrderBookDto
{
    [JsonPropertyName("product_id")]
    public string ProductId { get; init; } = string.Empty;

    [JsonPropertyName("bids")]
    public List<OrderBookEntryDto> Bids { get; init; } = [];

    [JsonPropertyName("asks")]
    public List<OrderBookEntryDto> Asks { get; init; } = [];

    /// <summary>Snapshot timestamp in RFC3339 format.</summary>
    [JsonPropertyName("time")]
    public string Time { get; init; } = string.Empty;
}
