namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>A single price-level entry in a Coinbase order book (<c>/api/v3/brokerage/market/product_book</c>).</summary>
internal sealed record OrderBookEntryDto
{
    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";

    [JsonPropertyName("size")]
    public string Size { get; init; } = "0";
}
