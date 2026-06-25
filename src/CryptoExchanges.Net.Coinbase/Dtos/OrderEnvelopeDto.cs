namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>The <c>/api/v3/brokerage/orders/historical/{order_id}</c> single-order response envelope.</summary>
internal sealed record OrderEnvelopeDto
{
    [JsonPropertyName("order")]
    public OrderDto? Order { get; init; }
}
