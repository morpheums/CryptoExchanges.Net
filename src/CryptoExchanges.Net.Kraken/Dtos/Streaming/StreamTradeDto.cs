namespace CryptoExchanges.Net.Kraken.Dtos.Streaming;

/// <summary>
/// One element of the <c>data</c> array in a Kraken WS v2 trade frame (<c>channel: trade</c>).
/// </summary>
internal sealed record StreamTradeDto
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("trade_id")]
    public long TradeId { get; init; }

    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    [JsonPropertyName("qty")]
    public decimal Qty { get; init; }

    /// <summary>Taker side: <c>"buy"</c> or <c>"sell"</c>.</summary>
    [JsonPropertyName("side")]
    public string Side { get; init; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = string.Empty;

    [JsonPropertyName("ord_type")]
    public string OrdType { get; init; } = string.Empty;
}
