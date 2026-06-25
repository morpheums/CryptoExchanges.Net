namespace CryptoExchanges.Net.Kraken.Dtos;

/// <summary>
/// Kraken <c>/0/private/ClosedOrders</c> result wrapper. Closed orders are nested inside a <c>closed</c>
/// dictionary keyed by order transaction id.
/// </summary>
internal sealed record ClosedOrdersResultDto
{
    [JsonPropertyName("closed")]
    public Dictionary<string, OrderDto> Closed { get; init; } = [];

    [JsonPropertyName("count")]
    public int Count { get; init; }
}
