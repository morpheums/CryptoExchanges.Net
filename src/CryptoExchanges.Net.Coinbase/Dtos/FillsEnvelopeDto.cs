namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>The <c>/api/v3/brokerage/orders/historical/fills</c> response envelope.</summary>
internal sealed record FillsEnvelopeDto
{
    [JsonPropertyName("fills")]
    public List<FillDto> Fills { get; init; } = [];
}
