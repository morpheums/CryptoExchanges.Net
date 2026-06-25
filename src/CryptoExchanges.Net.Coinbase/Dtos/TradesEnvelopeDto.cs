namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>The <c>/api/v3/brokerage/market/market-trades</c> response envelope.</summary>
internal sealed record TradesEnvelopeDto
{
    [JsonPropertyName("trades")]
    public List<TradeDto> Trades { get; init; } = [];
}
