namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>The <c>/api/v3/brokerage/orders/historical/batch</c> response envelope.</summary>
internal sealed record OrdersEnvelopeDto
{
    [JsonPropertyName("orders")]
    public List<OrderDto> Orders { get; init; } = [];
}
