namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>A single price-level entry in a Coinbase order book (<c>/api/v3/brokerage/product_book</c>).</summary>
internal sealed record OrderBookEntryDto
{
    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";

    [JsonPropertyName("size")]
    public string Size { get; init; } = "0";
}

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
