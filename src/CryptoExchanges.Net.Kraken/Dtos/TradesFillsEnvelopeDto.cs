namespace CryptoExchanges.Net.Kraken.Dtos;

/// <summary>
/// Wrapper for the Kraken <c>/0/private/TradesHistory</c> result payload, which nests fills inside
/// a <c>trades</c> dictionary keyed by trade transaction id.
/// </summary>
internal sealed record TradesFillsEnvelopeDto
{
    [JsonPropertyName("trades")]
    public Dictionary<string, FillDto> Trades { get; init; } = [];

    [JsonPropertyName("count")]
    public int Count { get; init; }
}
