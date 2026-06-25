namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>The <c>/api/v3/brokerage/product_book</c> response envelope.</summary>
internal sealed record OrderBookEnvelopeDto
{
    [JsonPropertyName("pricebook")]
    public OrderBookDto Pricebook { get; init; } = new();
}
