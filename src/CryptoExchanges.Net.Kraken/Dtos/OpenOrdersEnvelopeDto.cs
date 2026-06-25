namespace CryptoExchanges.Net.Kraken.Dtos;

/// <summary>
/// Kraken <c>/0/private/OpenOrders</c> result wrapper. Open orders are nested inside an <c>open</c>
/// dictionary keyed by order transaction id.
/// </summary>
internal sealed record OpenOrdersEnvelopeDto
{
    [JsonPropertyName("open")]
    public Dictionary<string, OrderDto> Open { get; init; } = [];
}
