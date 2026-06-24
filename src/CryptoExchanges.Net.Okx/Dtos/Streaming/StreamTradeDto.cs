namespace CryptoExchanges.Net.Okx.Dtos.Streaming;

/// <summary>
/// One element of the <c>data</c> array in an OKX WebSocket trade frame (<c>channel: trades</c>).
/// Symbol is sourced from <c>arg.instId</c> on the outer envelope.
/// </summary>
internal sealed record StreamTradeDto
{
    [JsonPropertyName("instId")]
    public string InstId { get; init; } = string.Empty;

    [JsonPropertyName("tradeId")]
    public string TradeId { get; init; } = string.Empty;

    [JsonPropertyName("px")]
    public string Px { get; init; } = "0";

    [JsonPropertyName("sz")]
    public string Sz { get; init; } = "0";

    [JsonPropertyName("side")]
    public string Side { get; init; } = string.Empty;

    [JsonPropertyName("ts")]
    public string Ts { get; init; } = "0";
}
