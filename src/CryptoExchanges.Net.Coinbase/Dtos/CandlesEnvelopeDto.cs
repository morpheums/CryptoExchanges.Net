namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>The <c>/api/v3/brokerage/products/{product_id}/candles</c> response envelope.</summary>
internal sealed record CandlesEnvelopeDto
{
    [JsonPropertyName("candles")]
    public List<CandlestickDto> Candles { get; init; } = [];
}
