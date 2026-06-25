namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>The <c>POST /api/v3/brokerage/orders/batch_cancel</c> response.</summary>
internal sealed record CancelOrdersEnvelopeDto
{
    [JsonPropertyName("results")]
    public List<CancelOrderEntryDto> Results { get; init; } = [];
}
